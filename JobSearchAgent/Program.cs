using JobSearchAgent.Agents;
using JobSearchAgent.Data;
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

// Load secrets: dotnet user-secrets first, then environment variables, then .env fallback
var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .AddDotEnvFile()
    .Build();

string apiKey = config["ANTHROPIC_API_KEY"]
    ?? throw new InvalidOperationException(
        "ANTHROPIC_API_KEY not set. Run: dotnet user-secrets set ANTHROPIC_API_KEY <key>");

string credentialsFile = config["GMAIL_CREDENTIALS_PATH"] ?? "credentials.json";
string credentialsPath = FindFileInAncestors(credentialsFile)
    ?? throw new FileNotFoundException(
        $"Could not find '{credentialsFile}' in any ancestor directory. " +
        "Place credentials.json in the repo root or set GMAIL_CREDENTIALS_PATH to its absolute path.");
string tokenStorePath = Path.GetDirectoryName(credentialsPath)!;

// Init database
await using var db = new AppDbContext();
await db.Database.EnsureCreatedAsync();
await db.Database.ExecuteSqlRawAsync("""
    CREATE TABLE IF NOT EXISTS "Classifications" (
        "Id"           INTEGER NOT NULL CONSTRAINT "PK_Classifications" PRIMARY KEY AUTOINCREMENT,
        "MessageId"    TEXT    NOT NULL,
        "IsJobRelated" INTEGER NOT NULL,
        "Category"     TEXT    NOT NULL,
        "Confidence"   REAL    NOT NULL,
        "Company"      TEXT    NOT NULL,
        "RoleTitle"    TEXT    NOT NULL,
        "ClassifiedAt" TEXT    NOT NULL
    )
    """);
await db.Database.ExecuteSqlRawAsync("""
    CREATE UNIQUE INDEX IF NOT EXISTS "IX_Classifications_MessageId" ON "Classifications" ("MessageId")
    """);

// Auth Gmail
var gmail = await GmailClient.CreateAsync(credentialsPath, tokenStorePath);
Console.WriteLine("Gmail authenticated.");

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
    // Plus any stored emails that were never classified (e.g. stored before classification was added)
    var unclassified = EmailRepository.GetUnclassified(db);
    var seen = new HashSet<string>(fresh.Select(e => e.MessageId));
    emailsToClassify = fresh.Concat(unclassified.Where(e => !seen.Contains(e.MessageId))).ToList();
}

Console.WriteLine($"Fetched {emails.Count} — classifying {emailsToClassify.Count}...");

if (emailsToClassify.Count == 0)
{
    Console.WriteLine("Nothing to classify.");
    return;
}

var classifier = new EmailClassifier(apiKey);
var results = await classifier.ClassifyBatchAsync(emailsToClassify);

EmailRepository.SaveClassifications(db, results.Select(r => (r.Email.MessageId, r.Classification)));

var jobRelated = results.Where(r => r.Classification.IsJobRelated).ToList();
int notRelevantCount = results.Count - jobRelated.Count;

Console.WriteLine();
Console.WriteLine($"Results: {jobRelated.Count} job-related, {notRelevantCount} not relevant.");

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

// ---------------------------------------------------------------------------
// Walk up ancestor directories to find a file by name or relative path.
// Works regardless of whether the app is launched via 'dotnet run', the
// debugger (CWD = bin/Debug/net9.0), or a published binary.
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

// ---------------------------------------------------------------------------
// Minimal .env reader — lets the Python project's .env work as a fallback
// ---------------------------------------------------------------------------
static class DotEnvExtensions
{
    public static IConfigurationBuilder AddDotEnvFile(this IConfigurationBuilder builder)
    {
        // Look for .env one level up (root of the repo alongside the Python project)
        string envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", ".env");
        if (!File.Exists(envPath)) return builder;

        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(envPath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#')) continue;
            int eq = line.IndexOf('=');
            if (eq < 0) continue;
            dict[line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }
        return builder.AddInMemoryCollection(dict);
    }
}
