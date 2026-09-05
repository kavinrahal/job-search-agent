using System.Globalization;
using JobSearch.Data;
using JobSearchAgent.Agents;
using JobSearchAgent.Integrations;
using JobSearchAgent.Models;
using JobSearchAgent.Workers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Parse arguments
//   --days N            last N days (test mode — classifies all fetched)
//   --from YYYY-MM-DD   explicit start date (test mode)
//   --to   YYYY-MM-DD   optional end date, used with --from
int? days = null;
DateTimeOffset? fromDate = null;
DateTimeOffset? toDate = null;

for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--days" && int.TryParse(args[i + 1], out int d))
        days = d;
    else if (args[i] == "--from" && DateTimeOffset.TryParse(args[i + 1], CultureInfo.InvariantCulture, out var f))
        fromDate = f;
    else if (args[i] == "--to" && DateTimeOffset.TryParse(args[i + 1], CultureInfo.InvariantCulture, out var t))
        toDate = t;
}
bool testMode = days.HasValue || fromDate.HasValue;

// Load secrets: dotnet user-secrets first, then environment variables
var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

string apiKey = config["ANTHROPIC_API_KEY"]
    ?? throw new InvalidOperationException(
        "ANTHROPIC_API_KEY not set. Run: dotnet user-secrets set ANTHROPIC_API_KEY <key>");

var runStart = DateTime.UtcNow;

// Crash reporting. Absent DSN = disabled (normal locally and in CI); only the deployed cron
// service sets SENTRY_DSN. Initialized before the database work below so a migration or
// connection failure — which would otherwise only ever surface in Railway logs that don't
// survive the next container boot — still produces a durable, alertable event.
using var sentry = SentryConfig.TryInitConsole(config["SENTRY_DSN"], "production");

// Init database — connection string from user-secrets / DATABASE_URL env var / local default.
// Smaller pool than the API's: users are processed sequentially, so this rarely needs more
// than one or two connections open at once even accounting for async overlap.
string connStr = AppDbContext.GetConnectionString(config.GetConnectionString("DefaultConnection"), maxPoolSize: 10);
var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(connStr)
    .Options;
await using var db = new AppDbContext(dbOptions);
await db.Database.MigrateAsync();

// Guards against two runs overlapping if a cron trigger fires before the previous run
// finished — see WorkerLockService for why a plain load-then-save check is fine here.
if (!await WorkerLockService.TryAcquireAsync(db, DateTime.UtcNow))
{
    Console.WriteLine("Another run is still in progress — skipping this trigger.");
    return;
}

// Data Protection key ring persisted to Postgres (not the framework's local-disk default)
// since this worker and JobSearch.Api are separate processes on ephemeral containers that
// both need to decrypt UserSecrets encrypted by either one.
var dataProtectionServices = new ServiceCollection();
dataProtectionServices.AddDbContext<AppDbContext>(o => o.UseNpgsql(connStr));
dataProtectionServices.AddDataProtection().PersistKeysToDbContext<AppDbContext>().SetApplicationName("JobFindr");
var userSecrets = new UserSecretService(dataProtectionServices.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>());

// Shared across every user processed this run — creates its own fresh AppDbContext per
// log write, so it's safe to reuse across the whole loop below.
var usageLogger = new ClaudeUsageLogger(dbOptions);

// clientId/clientSecret are the app's own Gmail OAuth client (shared across every user's
// Gmail connection, like GOOGLE_CLIENT_ID for the web login) — stays an env var. Each
// user's own refresh token lives encrypted in UserSecrets.
var clientId     = config["GMAIL_CLIENT_ID"];
var clientSecret = config["GMAIL_CLIENT_SECRET"];

// Filter tracking mode's Gmail Settings API access (installing per-domain forwarding
// filters) and the SendGrid inbound domain their forwarded mail arrives through — same
// client credentials as GmailClient above, just a narrower scope (see GmailSettingsClient).
var gmailSettingsClient = clientId is not null && clientSecret is not null
    ? new GmailSettingsClient(clientId, clientSecret)
    : null;
var sendGridInboundDomain = config["SENDGRID_INBOUND_DOMAIN"];

var adzunaAppId  = config["ADZUNA_APP_ID"];
var adzunaAppKey = config["ADZUNA_APP_KEY"];

// Every user has an email from their Google login, so this goes to any Tier2 user a match
// is found for. Same SENDGRID_API_KEY/SENDGRID_FROM_EMAIL config the beta-invite email
// already uses (Mail Send permission — separate from SENDGRID_INBOUND_SECRET, which only
// ever receives).
var sendGridApiKey = config["SENDGRID_API_KEY"];
var sendGridFromEmail = config["SENDGRID_FROM_EMAIL"];
var emailer = sendGridApiKey is not null && sendGridFromEmail is not null
    ? new SendGridEmailService(sendGridApiKey, sendGridFromEmail)
    : null;

// ---------------------------------------------------------------------------
// Owner bootstrap — guarantees the owner is an "active user" (has a UserProfile and a
// stored Gmail refresh token) by the time the loop below queries for active users. Only
// the owner goes through this: env-var/file-seeded data and the interactive browser-auth
// fallback are one-person, local-setup concerns, not something a real second user needs —
// they'll onboard through a proper OAuth consent flow in a future ticket.
// ---------------------------------------------------------------------------
var ownerEmail = config["ALLOWED_EMAIL"] ?? "kavinrahal@gmail.com";
var owner = await UserProvisioningService.GetOrCreateAsync(db, ownerEmail, UserTier.Tier2, 1_000_000);

await UserProfileProvisioningService.GetOrSeedAsync(db, owner.Id,
    background: SkillLoader.Load("context/background.yaml"),
    cvBase: SkillLoader.Load("context/cv_base.md"),
    jobCriteria: SkillLoader.Load("context/job_criteria.yaml"));

var ownerRefreshToken = await userSecrets.GetAsync(db, owner.Id, UserSecretKey.GmailRefreshToken);
if (ownerRefreshToken is null && config["GMAIL_REFRESH_TOKEN"] is string envRefreshToken)
{
    await userSecrets.SetAsync(db, owner.Id, UserSecretKey.GmailRefreshToken, envRefreshToken);
    Console.WriteLine("Migrated GMAIL_REFRESH_TOKEN into encrypted per-user storage.");
}
else if (ownerRefreshToken is null && clientId is null)
{
    // Nothing headless available at all — one-time interactive local setup. Extracts a
    // refresh token via the browser flow and stores it, so every run after this is headless.
    string credentialsFile = config["GMAIL_CREDENTIALS_PATH"] ?? "credentials.json";
    string credentialsPath = FindFileInAncestors(credentialsFile)
        ?? throw new FileNotFoundException(
            $"Could not find '{credentialsFile}'. Set GMAIL_CLIENT_ID/SECRET/REFRESH_TOKEN in user-secrets, " +
            "or place credentials.json in the repo root for first-time browser auth.");
    string tokenStorePath = Path.GetDirectoryName(credentialsPath)!;
    var (bootstrapClientId, bootstrapClientSecret, bootstrapRefreshToken) =
        await GmailClient.AuthorizeWithBrowserFlowAsync(credentialsPath, tokenStorePath);
    clientId = bootstrapClientId;
    clientSecret = bootstrapClientSecret;
    await userSecrets.SetAsync(db, owner.Id, UserSecretKey.GmailRefreshToken, bootstrapRefreshToken);
    Console.WriteLine("Gmail authenticated (browser flow) — refresh token stored for future headless runs.");
}

// ---------------------------------------------------------------------------
// Tier 2 aggregator/ATS discovery: runs the moment someone is Tier 2, independent of
// whether they've connected Gmail — Gmail is a separate, optional step (application status
// tracking), not a prerequisite for seeing discovered postings. Deliberately not folded into
// the Gmail-gated loop below.
// ---------------------------------------------------------------------------
if (!testMode)
{
    // A UserProfile row alone isn't enough — every user gets one auto-seeded blank (empty
    // Background/JobCriteria) on their very first login, before they've done any onboarding.
    // Without real criteria, evaluate_posting.md has no disqualifiers or skill dimensions to
    // check a posting against, so the model has nothing to reject an irrelevant posting on —
    // a live incident where an incomplete-profile user got emailed about a role from a
    // completely unrelated profession.
    //
    // Beyond that, target_job_titles specifically must be filled in too — this is what
    // Adzuna's proactive search actually runs against (see RunDiscoveryForUserAsync /
    // TargetJobTitles.Parse), and it's a deliberately explicit, user-typed field rather than
    // something inferred from the rest of criteria: an AI-derived guess at "what should we
    // search for" was the earlier version of this, and it still risked searching for the
    // wrong thing when criteria was thin. No titles means no legitimate search to run, so
    // discovery is skipped for that user entirely rather than guessing.
    //
    // JobCriteria has to be fetched here (not just checked for existence via Any()) since
    // TargetJobTitles.Parse needs the actual text — filtered in-memory after, since EF Core
    // can't translate that parse into SQL.
    // DeactivatedAt == null matters here specifically: unlike activeUsers/filterModeUsers below,
    // nothing about discovery eligibility depends on a Gmail secret (cancellation always revokes
    // and deletes those, which incidentally drops a cancelled user out of those two queries on
    // its own) — Tier2 + EnabledSources + JobCriteria are untouched by /account/cancel, so
    // without this filter a cancelled Tier2 user's automatic discovery (real Claude API spend,
    // real email notifications) kept running indefinitely after cancellation. Confirmed live on
    // staging during the pre-launch cancellation audit.
    var discoveryCandidates = await db.Users
        .Where(u => u.Tier == UserTier.Tier2 && u.DeactivatedAt == null)
        .Select(u => new
        {
            User = u,
            JobCriteria = db.UserProfiles.Where(p => p.UserId == u.Id).Select(p => p.JobCriteria).FirstOrDefault(),
        })
        .ToListAsync();

    var discoveryUsers = discoveryCandidates
        .Where(c => !string.IsNullOrEmpty(c.JobCriteria) && TargetJobTitles.Parse(c.JobCriteria).Length > 0)
        .Select(c => c.User)
        .ToList();

    Console.WriteLine($"Running aggregator discovery for {discoveryUsers.Count} Tier 2 user(s)...");
    foreach (var user in discoveryUsers)
    {
        try
        {
            var (discovered, evaluated, notified) = await RunDiscoveryForUserAsync(user);
            Console.WriteLine($"  [{user.Email}] discovery: {discovered} new, {evaluated} evaluated, {notified} notified.");
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[{user.Email}] Discovery error: {ex}");
        }
    }
}

// ---------------------------------------------------------------------------
// Active users: Gmail connected AND criteria actually filled in (not just a UserProfile row
// existing — see the comment on discoveryUsers above for why that alone isn't enough).
// Anyone missing either is skipped for now — no partial-pipeline branching for a case with no
// real users yet.
// ---------------------------------------------------------------------------
// DeactivatedAt == null is belt-and-suspenders here — /account/cancel already revokes and
// deletes the GmailRefreshToken secret unconditionally, which normally drops a cancelled user
// out of this query on its own — but that's an incidental side effect of cancellation's Gmail
// cleanup, not something this query should have to rely on to stay correct.
var activeUsers = await db.Users
    .Where(u => u.DeactivatedAt == null
             && db.UserProfiles.Any(p => p.UserId == u.Id && p.JobCriteria != null && p.JobCriteria != "")
             && db.UserSecrets.Any(s => s.UserId == u.Id && s.Key == UserSecretKey.GmailRefreshToken))
    .ToListAsync();

Console.WriteLine($"Processing {activeUsers.Count} active user(s)...");

int totalEmailsFetched = 0, totalEmailsClassified = 0, totalNewApplications = 0;

foreach (var user in activeUsers)
{
    Console.WriteLine();
    Console.WriteLine($"=== {user.Email} ===");
    try
    {
        var result = await ProcessUserAsync(user);
        totalEmailsFetched += result.EmailsFetched;
        totalEmailsClassified += result.EmailsClassified;
        totalNewApplications += result.NewApplications;
    }
    catch (Exception ex)
    {
        // One user's Gmail hiccup or API failure must not stall everyone else. Swallowing it
        // here is deliberate, but it also meant real bugs (e.g. the RawEmails duplicate-key
        // crash) only ever appeared in Railway logs that don't survive the next container
        // boot — so it's reported before being swallowed. Scoped to the user id, never the
        // email address, per SentryConfig's scrubbing rules.
        SentrySdk.CaptureException(ex, scope => scope.User = new SentryUser { Id = user.Id.ToString() });
        await Console.Error.WriteLineAsync($"[{user.Email}] Unhandled error: {ex}");
    }
}

// ---------------------------------------------------------------------------
// Filter-mode users: gmail.settings.basic scope only, no inbox read access. Their forwarded
// mail already arrived via the SendGrid inbound webhook (see /api/v1/sendgrid/inbound) and
// sits in RawEmails unclassified — nothing to fetch from Gmail here, just classify what's
// already stored and hand it to the same tracking/auto-capture pipeline full-mode users use.
// ---------------------------------------------------------------------------
if (!testMode)
{
    // Same belt-and-suspenders DeactivatedAt filter as activeUsers above.
    var filterModeUsers = await db.Users
        .Where(u => u.DeactivatedAt == null
                 && u.GmailTrackingMode == GmailTrackingMode.Filter
                 && db.UserProfiles.Any(p => p.UserId == u.Id && p.JobCriteria != null && p.JobCriteria != "")
                 && db.UserSecrets.Any(s => s.UserId == u.Id && s.Key == UserSecretKey.GmailSettingsRefreshToken))
        .ToListAsync();

    Console.WriteLine($"Processing {filterModeUsers.Count} filter-mode user(s)...");

    foreach (var user in filterModeUsers)
    {
        Console.WriteLine();
        Console.WriteLine($"=== {user.Email} (filter mode) ===");
        try
        {
            await using var userDb = new AppDbContext(dbOptions) { CurrentUserId = user.Id };
            var unclassified = userDb.RawEmails
                .Where(r => !userDb.Classifications.Any(c => c.MessageId == r.MessageId))
                .AsEnumerable()
                .Select(r => new RawEmail(
                    r.MessageId, r.ThreadId, r.FromAddress, r.Subject, r.BodyText,
                    new DateTimeOffset(r.ReceivedAt, TimeSpan.Zero)))
                .ToList();

            if (unclassified.Count == 0)
            {
                Console.WriteLine("Nothing to classify.");
                continue;
            }

            Console.WriteLine($"Classifying {unclassified.Count}...");
            var (results, tracking) = await ClassifyAndTrackAsync(userDb, user, unclassified);
            totalEmailsClassified += results.Count;
            totalNewApplications += tracking.Created;
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex, scope => scope.User = new SentryUser { Id = user.Id.ToString() });
            await Console.Error.WriteLineAsync($"[{user.Email}] Unhandled error (filter mode): {ex}");
        }
    }
}

db.SystemHealth.Add(new SystemHealth
{
    CheckedAt = DateTime.UtcNow,
    EmailsFetched = totalEmailsFetched,
    EmailsClassified = totalEmailsClassified,
    NewApplications = totalNewApplications,
    DurationMs = (int)(DateTime.UtcNow - runStart).TotalMilliseconds,
});
await db.SaveChangesAsync();

await WorkerLockService.ReleaseAsync(db);

// ---------------------------------------------------------------------------
// Aggregator/ATS discovery for one Tier 2 user — decoupled from Gmail so it starts the
// moment they're Tier 2, no OAuth prerequisite. Fresh AppDbContext per user, same reasoning
// as ProcessUserAsync below.
// ---------------------------------------------------------------------------
async Task<(int Discovered, int Evaluated, int Notified)> RunDiscoveryForUserAsync(User user)
{
    await using var userDb = new AppDbContext(dbOptions) { CurrentUserId = user.Id };

    // discoveryUsers (see the query above) already requires target_job_titles to be filled
    // in, so this should always find real titles to search for — re-parsed here rather than
    // threaded through from the eligibility check, since that's a cheap regex, not worth
    // carrying state across for.
    var profile = await userDb.UserProfiles.FindAsync(user.Id);
    var adzunaKeywords = TargetJobTitles.Parse(profile?.JobCriteria);

    var fetchers = DiscoverySourceResolver.Resolve(user.EnabledSources, adzunaAppId, adzunaAppKey, adzunaKeywords);
    if (fetchers.Count == 0)
    {
        Console.WriteLine("    (no automatic sources enabled — skipped)");
        return (0, 0, 0);
    }

    var discovery = new JobDiscoveryWorker(
        userDb, fetchers, new JobPostingFetcher(), new PostingEvaluator(apiKey, usageLogger), emailer);
    return await discovery.RunAsync();
}

// ---------------------------------------------------------------------------
// Per-user pipeline: fetch, classify, track applications, process job alerts, send
// notifications. Runs against its own AppDbContext (fresh per user, not the bootstrap `db`
// above) so tracked entities and CurrentUserId never leak between users.
// ---------------------------------------------------------------------------
async Task<(int EmailsFetched, int EmailsClassified, int NewApplications)> ProcessUserAsync(User user)
{
    await using var userDb = new AppDbContext(dbOptions) { CurrentUserId = user.Id };

    var refreshToken = await userSecrets.GetAsync(userDb, user.Id, UserSecretKey.GmailRefreshToken);
    if (refreshToken is null || clientId is null || clientSecret is null)
    {
        Console.WriteLine("Gmail not fully configured — skipped.");
        return (0, 0, 0);
    }
    var gmail = await GmailClient.CreateAsync(clientId, clientSecret, refreshToken);

    // Determine fetch window
    DateTimeOffset? since;
    if (fromDate.HasValue)
    {
        since = fromDate;
        string label = toDate.HasValue
            ? $"{fromDate.Value:yyyy-MM-dd} → {toDate.Value:yyyy-MM-dd}"
            : $"from {fromDate.Value:yyyy-MM-dd}";
        Console.WriteLine($"Date range mode: {label}...");
    }
    else if (days.HasValue)
    {
        since = DateTimeOffset.UtcNow.AddDays(-days.Value);
        Console.WriteLine($"Test mode: fetching last {days} days...");
    }
    else
    {
        var latestRaw = await userDb.RawEmails.OrderByDescending(r => r.ReceivedAt).Select(r => (DateTime?)r.ReceivedAt).FirstOrDefaultAsync();
        since = latestRaw is DateTime d ? new DateTimeOffset(d, TimeSpan.Zero) : null;
        string label = since.HasValue
            ? since.Value.ToString("yyyy-MM-dd HH:mm UTC")
            : "last 24 hours";
        Console.WriteLine($"Fetching emails since {label}...");
    }

    var emails = await gmail.FetchEmailsSinceAsync(since, until: toDate);

    // Batch upsert — one query to find existing, one SaveChanges for all new rows
    var fetchedIds = emails.Select(e => e.MessageId).ToHashSet();
    var existingIds = await userDb.RawEmails
        .Where(r => fetchedIds.Contains(r.MessageId))
        .Select(r => r.MessageId)
        .ToHashSetAsync();
    foreach (var email in emails.Where(e => !existingIds.Contains(e.MessageId)))
    {
        userDb.RawEmails.Add(new RawEmailRecord
        {
            UserId      = user.Id,
            MessageId   = email.MessageId,
            ThreadId    = email.ThreadId,
            FromAddress = email.FromAddress,
            Subject     = email.Subject,
            BodyText    = email.BodyText,
            ReceivedAt  = email.ReceivedAt.UtcDateTime,
        });
    }
    await userDb.SaveChangesAsync();

    // Determine what to classify
    List<RawEmail> emailsToClassify;
    if (testMode)
    {
        emailsToClassify = emails;
    }
    else
    {
        var fresh = since.HasValue ? emails.Where(e => e.ReceivedAt > since.Value).ToList() : emails;
        var unclassified = userDb.RawEmails
            .Where(r => !userDb.Classifications.Any(c => c.MessageId == r.MessageId))
            .AsEnumerable()
            .Select(r => new RawEmail(
                r.MessageId, r.ThreadId, r.FromAddress, r.Subject, r.BodyText,
                new DateTimeOffset(r.ReceivedAt, TimeSpan.Zero)))
            .ToList();
        var seen = new HashSet<string>(fresh.Select(e => e.MessageId));
        emailsToClassify = fresh.Concat(unclassified.Where(e => !seen.Contains(e.MessageId))).ToList();
    }

    Console.WriteLine($"Fetched {emails.Count} — classifying {emailsToClassify.Count}...");

    if (emailsToClassify.Count == 0)
    {
        Console.WriteLine("Nothing to classify.");
        if (!testMode)
            await RunAlertProcessingAsync(userDb);
        return (emails.Count, 0, 0);
    }

    var (results, tracking) = await ClassifyAndTrackAsync(userDb, user, emailsToClassify);

    var jobRelated = results.Where(r => r.Classification.IsJobRelated).ToList();
    int notRelevantCount = results.Count - jobRelated.Count;

    Console.WriteLine();
    Console.WriteLine($"Results: {jobRelated.Count} job-related, {notRelevantCount} not relevant.");

    // Process job alert emails — query all stored alerts so previously-classified
    // ones are retried each run (dedup in JobAlertProcessor handles already-done URLs).
    if (!testMode)
        await RunAlertProcessingAsync(userDb);

    if (jobRelated.Count > 0)
    {
        var categoryLabels = new Dictionary<string, string>
        {
            ["application_confirmation"] = "Application confirmed",
            ["rejection"]                = "Rejection",
            ["interview_invitation"]     = "Interview invite",
            ["recruiter_outreach"]       = "Recruiter outreach",
            ["scheduling_request"]       = "Scheduling request",
            ["offer"]                    = "Offer",
            ["follow_up_needed"]         = "Action needed",
            ["job_alert"]                = "Job alert",
            ["not_relevant"]             = "Not relevant",
        };

        Console.WriteLine();
        Console.WriteLine("Job-search emails:");
        foreach (var (email, clf) in jobRelated)
        {
            string ts = email.ReceivedAt.ToString("MM-dd HH:mm");
            string tag = categoryLabels.GetValueOrDefault(clf.Category, clf.Category);
            string company = clf.Company.Length > 0 ? $" [{clf.Company}]" : "";
            string role = clf.RoleTitle.Length > 0 ? $" — {clf.RoleTitle}" : "";
            string subject = email.Subject.Length > 80 ? email.Subject[..80] : email.Subject;
            Console.WriteLine($"  [{ts}] {tag}{company}{role}");
            Console.WriteLine($"         {subject}");
        }
    }
    else
    {
        Console.WriteLine("No job-search emails found in this window.");
    }

    return (emails.Count, emailsToClassify.Count, tracking.Created);
}

// ---------------------------------------------------------------------------
// Classify a batch of emails, persist the classifications, run application tracking, then
// two follow-ups that both key off the classification results: acknowledgment-domain
// auto-capture (filter mode only — see AcknowledgmentDomainCapture) and the retention scrub.
// Shared by both the full-mode (readonly-fetched) and filter-mode (webhook-forwarded) paths
// above — the only difference between them is where emailsToClassify came from.
// ---------------------------------------------------------------------------
async Task<(List<(RawEmail Email, EmailClassification Classification)> Results, TrackingResult Tracking)> ClassifyAndTrackAsync(
    AppDbContext userDb, User user, List<RawEmail> emailsToClassify)
{
    var classifier = new EmailClassifier(apiKey, usageLogger);
    var results = await classifier.ClassifyBatchAsync(emailsToClassify, user.Id);

    var now = DateTime.UtcNow;
    foreach (var (email, clf) in results)
    {
        var existing = await userDb.Classifications.FirstOrDefaultAsync(c => c.MessageId == email.MessageId);
        if (existing is not null)
        {
            existing.IsJobRelated = clf.IsJobRelated;
            existing.Category     = clf.Category;
            existing.Confidence   = clf.Confidence;
            existing.Company      = clf.Company;
            existing.RoleTitle    = clf.RoleTitle;
            existing.ClassifiedAt = now;
        }
        else
        {
            userDb.Classifications.Add(new ClassificationRecord
            {
                UserId       = user.Id,
                MessageId    = email.MessageId,
                IsJobRelated = clf.IsJobRelated,
                Category     = clf.Category,
                Confidence   = clf.Confidence,
                Company      = clf.Company,
                RoleTitle    = clf.RoleTitle,
                ClassifiedAt = now,
            });
        }
    }
    await userDb.SaveChangesAsync();

    var tracking = await ApplicationTracker.ProcessClassificationsAsync(userDb, results);
    if (tracking.Created > 0 || tracking.Updated > 0)
        Console.WriteLine($"Applications: {tracking.Created} created, {tracking.Updated} updated.");

    await AutoCaptureAcknowledgmentDomainsAsync(userDb, user, results);

    // Privacy: clear full body text for anything nothing will read again — see
    // RawEmailRetentionPolicy for which emails that is and why.
    var messageIdsToScrub = RawEmailRetentionPolicy.SelectMessageIdsToScrub(results);
    if (messageIdsToScrub.Count > 0)
    {
        var rawRecords = await userDb.RawEmails
            .Where(r => messageIdsToScrub.Contains(r.MessageId))
            .ToListAsync();
        foreach (var raw in rawRecords)
            raw.BodyText = "";
        await userDb.SaveChangesAsync();
    }

    return (results, tracking);
}

// ---------------------------------------------------------------------------
// Filter tracking mode only: an acknowledgment matched by AcknowledgmentFilterQuery's phrase
// half (not one of the pre-known ATS domains) gets that sender's domain installed as its own
// filter, so this specific sender is caught by domain — not phrasing — from now on. Best
// effort, same as EnsureCompanyFilterAsync's other call site: a failure here shouldn't affect
// the classification/tracking that already succeeded.
// ---------------------------------------------------------------------------
async Task AutoCaptureAcknowledgmentDomainsAsync(
    AppDbContext userDb, User user, List<(RawEmail Email, EmailClassification Classification)> results)
{
    if (gmailSettingsClient is null || sendGridInboundDomain is null) return;

    var domains = AcknowledgmentDomainCapture.SelectDomainsToCapture(results, GmailSettingsClient.KnownAckDomains);
    if (domains.Count == 0) return;

    var settingsRefreshToken = await userSecrets.GetAsync(userDb, user.Id, UserSecretKey.GmailSettingsRefreshToken);
    if (settingsRefreshToken is null) return;

    var address = await InboundEmailService.GetOrCreateAddressAsync(userDb, user.Id, sendGridInboundDomain);
    foreach (var domain in domains)
    {
        try
        {
            await gmailSettingsClient.EnsureCompanyFilterAsync(settingsRefreshToken, domain, address);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Acknowledgment domain auto-capture failed for user {user.Id}/{domain}: {ex}");
        }
    }
}

// ---------------------------------------------------------------------------
// Load all stored job_alert emails from DB and run the alert processor.
// ---------------------------------------------------------------------------
async Task RunAlertProcessingAsync(AppDbContext userDb)
{
    var alertMessageIds = await userDb.Classifications
        .Where(c => c.Category == "job_alert" && c.IsJobRelated)
        .Select(c => c.MessageId)
        .ToHashSetAsync();
    if (alertMessageIds.Count == 0) return;

    var allAlertEmails = userDb.RawEmails
        .Where(r => alertMessageIds.Contains(r.MessageId))
        .AsEnumerable()
        .Select(r => new RawEmail(
            r.MessageId, r.ThreadId, r.FromAddress, r.Subject, r.BodyText,
            new DateTimeOffset(r.ReceivedAt, TimeSpan.Zero)))
        .ToList();

    Console.WriteLine();
    var adzunaCrossCheckFetcher = adzunaAppId is not null && adzunaAppKey is not null
        ? new AdzunaFetcher(adzunaAppId, adzunaAppKey)
        : null;
    var alertProcessor = new JobAlertProcessor(
        userDb, new JobPostingFetcher(), new PostingEvaluator(apiKey, usageLogger),
        new JoraFetcher(), adzunaCrossCheckFetcher, new PostingMatcherAgent(apiKey, usageLogger), emailer);
    var (found, evaluated, notified) = await alertProcessor.ProcessAsync(allAlertEmails);
    Console.WriteLine($"Job alerts: {found} URLs found, {evaluated} evaluated, {notified} notified.");
}

// ---------------------------------------------------------------------------
// Walk up ancestor directories to find a file by name or relative path.
// ---------------------------------------------------------------------------
static string? FindFileInAncestors(string fileNameOrRelPath)
{
    if (Path.IsPathRooted(fileNameOrRelPath))
        return File.Exists(fileNameOrRelPath) ? fileNameOrRelPath : null;

    string fileName = Path.GetFileName(fileNameOrRelPath);
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir != null)
    {
        string candidate = Path.Combine(dir.FullName, fileName);
        if (File.Exists(candidate)) return candidate;
        dir = dir.Parent;
    }
    return null;
}
