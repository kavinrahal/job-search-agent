using JobSearch.Data;
using JobSearchAgent.Agents;
using JobSearchAgent.Integrations;
using JobSearchAgent.Models;
using JobSearchAgent.Storage;
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
    else if (args[i] == "--from" && DateTimeOffset.TryParse(args[i + 1], out var f))
        fromDate = f;
    else if (args[i] == "--to" && DateTimeOffset.TryParse(args[i + 1], out var t))
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
    since = EmailRepository.GetLatestReceivedAt(db);
    string label = since.HasValue
        ? since.Value.ToString("yyyy-MM-dd HH:mm UTC")
        : "last 24 hours";
    Console.WriteLine($"Fetching emails since {label}...");
}

var emails = await gmail.FetchEmailsSinceAsync(since, until: toDate);

// Always upsert (idempotent)
foreach (var email in emails)
    EmailRepository.UpsertRawEmail(db, email);

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
    var unclassified = EmailRepository.GetUnclassified(db);
    var seen = new HashSet<string>(fresh.Select(e => e.MessageId));
    emailsToClassify = fresh.Concat(unclassified.Where(e => !seen.Contains(e.MessageId))).ToList();
}

Console.WriteLine($"Fetched {emails.Count} — classifying {emailsToClassify.Count}...");

if (emailsToClassify.Count == 0)
{
    Console.WriteLine("Nothing to classify.");
    db.SystemHealth.Add(new JobSearch.Data.SystemHealth
    {
        CheckedAt = DateTime.UtcNow,
        EmailsFetched = emails.Count,
        EmailsClassified = 0,
        NewApplications = 0,
        DurationMs = (int)(DateTime.UtcNow - runStart).TotalMilliseconds,
    });
    db.SaveChanges();
    return;
}

var classifier = new EmailClassifier(apiKey);
var results = await classifier.ClassifyBatchAsync(emailsToClassify);

EmailRepository.SaveClassifications(db, results.Select(r => (r.Email.MessageId, r.Classification)));

var jobRelated = results.Where(r => r.Classification.IsJobRelated).ToList();
int notRelevantCount = results.Count - jobRelated.Count;

Console.WriteLine();
Console.WriteLine($"Results: {jobRelated.Count} job-related, {notRelevantCount} not relevant.");

var tracking = ApplicationTracker.ProcessClassifications(db, results);
if (tracking.Created > 0 || tracking.Updated > 0 || tracking.NotificationsQueued > 0)
    Console.WriteLine($"Applications: {tracking.Created} created, {tracking.Updated} updated, {tracking.NotificationsQueued} notifications queued.");

// Send any pending Telegram notifications (including ones queued by earlier runs that failed to send)
var botToken = config["TELEGRAM_BOT_TOKEN"];
var chatId   = config["TELEGRAM_CHAT_ID"];

if (botToken is not null && chatId is not null)
{
    var pending = db.Notifications.Where(n => n.SentAt == null).ToList();
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
        db.SaveChanges();
        Console.WriteLine($"Telegram: {sent}/{pending.Count} notification(s) sent.");
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
db.SaveChanges();

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
