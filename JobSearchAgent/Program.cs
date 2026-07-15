using System.Globalization;
using JobSearch.Data;
using JobSearchAgent.Agents;
using JobSearchAgent.Integrations;
using JobSearchAgent.Models;
using JobSearchAgent.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

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

// Load secrets: dotnet user-secrets first, then environment variables
var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

string apiKey = config["ANTHROPIC_API_KEY"]
    ?? throw new InvalidOperationException(
        "ANTHROPIC_API_KEY not set. Run: dotnet user-secrets set ANTHROPIC_API_KEY <key>");

var runStart = DateTime.UtcNow;

// Init database — connection string from user-secrets / DATABASE_URL env var / local default
string connStr = AppDbContext.GetConnectionString(config.GetConnectionString("DefaultConnection"));
var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(connStr)
    .Options;
await using var db = new AppDbContext(dbOptions);
await db.Database.MigrateAsync();

// Auth Gmail — headless if secrets are present, browser flow as first-time fallback
var clientId     = config["GMAIL_CLIENT_ID"];
var clientSecret = config["GMAIL_CLIENT_SECRET"];
var refreshToken = config["GMAIL_REFRESH_TOKEN"];

GmailClient gmail;
if (clientId is not null && clientSecret is not null && refreshToken is not null)
{
    gmail = await GmailClient.CreateAsync(clientId, clientSecret, refreshToken);
    Console.WriteLine("Gmail authenticated.");
}
else
{
    string credentialsFile = config["GMAIL_CREDENTIALS_PATH"] ?? "credentials.json";
    string credentialsPath = FindFileInAncestors(credentialsFile)
        ?? throw new FileNotFoundException(
            $"Could not find '{credentialsFile}'. Set GMAIL_CLIENT_ID/SECRET/REFRESH_TOKEN in user-secrets, " +
            "or place credentials.json in the repo root for first-time browser auth.");
    string tokenStorePath = Path.GetDirectoryName(credentialsPath)!;
    gmail = await GmailClient.CreateWithBrowserFlowAsync(credentialsPath, tokenStorePath);
    Console.WriteLine("Gmail authenticated (browser flow).");
}

// Determine fetch window
DateTimeOffset? since;
bool testMode = days.HasValue || fromDate.HasValue;

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
    var latestRaw = await db.RawEmails.OrderByDescending(r => r.ReceivedAt).Select(r => (DateTime?)r.ReceivedAt).FirstOrDefaultAsync();
    since = latestRaw is DateTime d ? new DateTimeOffset(d, TimeSpan.Zero) : null;
    string label = since.HasValue
        ? since.Value.ToString("yyyy-MM-dd HH:mm UTC")
        : "last 24 hours";
    Console.WriteLine($"Fetching emails since {label}...");
}

var emails = await gmail.FetchEmailsSinceAsync(since, until: toDate);

// Batch upsert — one query to find existing, one SaveChanges for all new rows
var fetchedIds = emails.Select(e => e.MessageId).ToHashSet();
var existingIds = await db.RawEmails
    .Where(r => fetchedIds.Contains(r.MessageId))
    .Select(r => r.MessageId)
    .ToHashSetAsync();
foreach (var email in emails.Where(e => !existingIds.Contains(e.MessageId)))
{
    db.RawEmails.Add(new RawEmailRecord
    {
        MessageId   = email.MessageId,
        ThreadId    = email.ThreadId,
        FromAddress = email.FromAddress,
        Subject     = email.Subject,
        BodyText    = email.BodyText,
        ReceivedAt  = email.ReceivedAt.UtcDateTime,
    });
}
await db.SaveChangesAsync();

// Determine what to classify
List<RawEmail> emailsToClassify;
if (testMode)
{
    emailsToClassify = emails;
}
else
{
    // Newly fetched emails newer than the checkpoint
    var fresh = since.HasValue ? emails.Where(e => e.ReceivedAt > since.Value).ToList() : emails;
    // Plus any stored emails that were never classified
    var unclassified = db.RawEmails
        .Where(r => !db.Classifications.Any(c => c.MessageId == r.MessageId))
        .AsEnumerable()
        .Select(r => new RawEmail(
            r.MessageId, r.ThreadId, r.FromAddress, r.Subject, r.BodyText,
            new DateTimeOffset(r.ReceivedAt, TimeSpan.Zero)))
        .ToList();
    var seen = new HashSet<string>(fresh.Select(e => e.MessageId));
    emailsToClassify = fresh.Concat(unclassified.Where(e => !seen.Contains(e.MessageId))).ToList();
}

Console.WriteLine($"Fetched {emails.Count} — classifying {emailsToClassify.Count}...");

var botToken = config["TELEGRAM_BOT_TOKEN"];
var chatId   = config["TELEGRAM_CHAT_ID"];

#pragma warning disable S1135 // TODO(whatsapp): pilot paused — blocked on Meta Business Verification
// (Account Restricted). Fully built and safe to leave as-is: every value below is
// optional and whatsappConfigured gates every send, so this stays a no-op until the
// env vars are set. To resume: complete verification, then follow the remaining
// setup steps (System User token, App Secret, webhook subscription, template
// submission).
#pragma warning restore S1135
//
// WhatsApp is an optional, parallel channel — Telegram must keep working even if
// WhatsApp is unset or misconfigured, so every value here is nullable and every
// construction site is null-checked, mirroring the Telegram pattern above.
var whatsappToken    = config["WHATSAPP_ACCESS_TOKEN"];
var whatsappPhoneId  = config["WHATSAPP_PHONE_NUMBER_ID"];
var whatsappToNumber = config["WHATSAPP_TO_NUMBER"];
var whatsappTemplate = config["WHATSAPP_TEMPLATE_NAME"] ?? "job_search_alert";
var whatsappLang     = config["WHATSAPP_TEMPLATE_LANG"] ?? "en_US";
bool whatsappConfigured = whatsappToken is not null && whatsappPhoneId is not null && whatsappToNumber is not null;

// ---------------------------------------------------------------------------
// Job discovery — always runs, even when there are no new emails to classify
// ---------------------------------------------------------------------------
var adzunaAppId  = config["ADZUNA_APP_ID"];
var adzunaAppKey = config["ADZUNA_APP_KEY"];

if (!testMode)
{
    Console.WriteLine();
    if (adzunaAppId is null || adzunaAppKey is null)
    {
        Console.WriteLine("Job discovery: skipped (ADZUNA_APP_ID / ADZUNA_APP_KEY not set).");
    }
    else
    {
        using var discoveryTelegram = botToken is not null && chatId is not null
            ? new TelegramNotifier(botToken, chatId)
            : null;
        using var discoveryWhatsApp = whatsappConfigured
            ? new WhatsAppNotifier(whatsappToken!, whatsappPhoneId!, whatsappToNumber!, whatsappTemplate, whatsappLang)
            : null;

        var discovery = new JobDiscoveryWorker(
            db,
            [
                new AdzunaFetcher(adzunaAppId, adzunaAppKey),
                new GreenhouseFetcher(),
                new LeverFetcher(),
            ],
            new JobPostingFetcher(),
            new PostingEvaluator(apiKey),
            discoveryTelegram,
            discoveryWhatsApp);

        var (discovered, evaluated, notified, whatsappNotified) = await discovery.RunAsync();
        Console.WriteLine($"Job discovery: {discovered} new, {evaluated} evaluated, {notified} notified (Telegram), {whatsappNotified} notified (WhatsApp).");
    }
}

if (emailsToClassify.Count == 0)
{
    Console.WriteLine("Nothing to classify.");
    if (!testMode)
        await RunAlertProcessing();
    db.SystemHealth.Add(new JobSearch.Data.SystemHealth
    {
        CheckedAt = DateTime.UtcNow,
        EmailsFetched = emails.Count,
        EmailsClassified = 0,
        NewApplications = 0,
        DurationMs = (int)(DateTime.UtcNow - runStart).TotalMilliseconds,
    });
    await db.SaveChangesAsync();
    return;
}

var classifier = new EmailClassifier(apiKey);
var results = await classifier.ClassifyBatchAsync(emailsToClassify);

var now = DateTime.UtcNow;
foreach (var (email, clf) in results)
{
    var existing = await db.Classifications.FirstOrDefaultAsync(c => c.MessageId == email.MessageId);
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
        db.Classifications.Add(new ClassificationRecord
        {
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
await db.SaveChangesAsync();

var jobRelated = results.Where(r => r.Classification.IsJobRelated).ToList();
int notRelevantCount = results.Count - jobRelated.Count;

Console.WriteLine();
Console.WriteLine($"Results: {jobRelated.Count} job-related, {notRelevantCount} not relevant.");

var tracking = await ApplicationTracker.ProcessClassificationsAsync(db, results);
if (tracking.Created > 0 || tracking.Updated > 0 || tracking.NotificationsQueued > 0)
    Console.WriteLine($"Applications: {tracking.Created} created, {tracking.Updated} updated, {tracking.NotificationsQueued} notifications queued.");

// Process job alert emails — query all stored alerts so previously-classified
// ones are retried each run (dedup in JobAlertProcessor handles already-done URLs).
if (!testMode)
    await RunAlertProcessing();

if (botToken is not null && chatId is not null)
{
    var pending = await db.Notifications.Where(n => n.SentAt == null).ToListAsync();
    if (pending.Count > 0)
    {
        using var telegram = new TelegramNotifier(botToken, chatId);
        var sentAt = DateTime.UtcNow;
        int sent = 0;
        foreach (var notification in pending)
        {
            if (await telegram.SendAsync(notification.Message))
            {
                notification.SentAt = sentAt;
                sent++;
            }
        }
        await db.SaveChangesAsync();
        Console.WriteLine($"Telegram: {sent}/{pending.Count} notification(s) sent.");
    }
}

if (whatsappConfigured)
{
    // Independent of Telegram's SentAt — a row already sent via Telegram still needs
    // its own WhatsApp attempt, and a WhatsApp failure must not block future retries.
    var pendingWhatsApp = await db.Notifications.Where(n => n.WhatsAppSentAt == null).ToListAsync();
    if (pendingWhatsApp.Count > 0)
    {
        using var whatsapp = new WhatsAppNotifier(whatsappToken!, whatsappPhoneId!, whatsappToNumber!, whatsappTemplate, whatsappLang);
        var sentAt = DateTime.UtcNow;
        int sent = 0;
        foreach (var notification in pendingWhatsApp)
        {
            var parts = notification.Message.Split('\n', 2);
            var label = parts[0];
            var detail = parts.Length > 1 ? parts[1] : "";
            var wamid = await whatsapp.SendTemplateAsync(label, detail);
            if (wamid is not null)
            {
                notification.WhatsAppSentAt = sentAt;
                notification.WhatsAppMessageId = wamid;
                sent++;
            }
        }
        await db.SaveChangesAsync();
        Console.WriteLine($"WhatsApp: {sent}/{pendingWhatsApp.Count} notification(s) sent.");
    }
}

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

db.SystemHealth.Add(new JobSearch.Data.SystemHealth
{
    CheckedAt = DateTime.UtcNow,
    EmailsFetched = emails.Count,
    EmailsClassified = emailsToClassify.Count,
    NewApplications = tracking.Created,
    DurationMs = (int)(DateTime.UtcNow - runStart).TotalMilliseconds,
});
await db.SaveChangesAsync();

// ---------------------------------------------------------------------------
// Load all stored job_alert emails from DB and run the alert processor.
// Running from DB (not just current-batch results) means previously-classified
// alert emails are retried each run — dedup in JobAlertProcessor skips done URLs.
// ---------------------------------------------------------------------------
async Task RunAlertProcessing()
{
    var alertMessageIds = await db.Classifications
        .Where(c => c.Category == "job_alert" && c.IsJobRelated)
        .Select(c => c.MessageId)
        .ToHashSetAsync();
    if (alertMessageIds.Count == 0) return;

    var allAlertEmails = db.RawEmails
        .Where(r => alertMessageIds.Contains(r.MessageId))
        .AsEnumerable()
        .Select(r => new RawEmail(
            r.MessageId, r.ThreadId, r.FromAddress, r.Subject, r.BodyText,
            new DateTimeOffset(r.ReceivedAt, TimeSpan.Zero)))
        .ToList();

    Console.WriteLine();
    using var alertTelegram = botToken is not null && chatId is not null
        ? new TelegramNotifier(botToken, chatId) : null;
    using var alertWhatsApp = whatsappConfigured
        ? new WhatsAppNotifier(whatsappToken!, whatsappPhoneId!, whatsappToNumber!, whatsappTemplate, whatsappLang) : null;
    var alertProcessor = new JobAlertProcessor(
        db, new JobPostingFetcher(), new PostingEvaluator(apiKey), alertTelegram, alertWhatsApp);
    var (found, evaluated, notified, whatsappNotified) = await alertProcessor.ProcessAsync(allAlertEmails);
    Console.WriteLine($"Job alerts: {found} URLs found, {evaluated} evaluated, {notified} notified (Telegram), {whatsappNotified} notified (WhatsApp).");
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
