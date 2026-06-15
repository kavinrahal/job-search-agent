using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Resolve the SQLite path — walk up from CWD if the configured path doesn't exist
string configuredPath = builder.Configuration["Database:SqlitePath"] ?? "job_search.db";
string dbPath = ResolveDbPath(configuredPath);

builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:5173", "http://localhost:3000")
     .AllowAnyHeader()
     .AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

// Ensure schema is current — EnsureCreated only works on a new DB,
// so we also run CREATE TABLE IF NOT EXISTS for tables added after initial creation.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
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
}

// ---------------------------------------------------------------------------
// GET /api/summary
// ---------------------------------------------------------------------------
app.MapGet("/api/summary", (AppDbContext db) =>
{
    var categories = db.Classifications
        .GroupBy(c => c.Category)
        .Select(g => new { Category = g.Key, Count = g.Count() })
        .ToList();

    return Results.Ok(new
    {
        total = db.RawEmails.Count(),
        classified = db.Classifications.Count(),
        jobRelated = db.Classifications.Count(c => c.IsJobRelated),
        byCategory = categories.ToDictionary(x => x.Category, x => x.Count),
    });
});

// ---------------------------------------------------------------------------
// GET /api/emails?page=1&pageSize=25&category=...&jobRelatedOnly=true&from=...&to=...
// ---------------------------------------------------------------------------
app.MapGet("/api/emails", (
    AppDbContext db,
    int page = 1,
    int pageSize = 25,
    string? category = null,
    bool? jobRelatedOnly = null,
    string? from = null,
    string? to = null) =>
{
    var query =
        from e in db.RawEmails
        join c in db.Classifications on e.MessageId equals c.MessageId into cls
        from c in cls.DefaultIfEmpty()
        select new { Email = e, Classification = c };

    if (from is not null && DateTime.TryParse(from, out var fromDate))
        query = query.Where(x => x.Email.ReceivedAt >= fromDate);

    if (to is not null && DateTime.TryParse(to, out var toDate))
        query = query.Where(x => x.Email.ReceivedAt <= toDate);

    if (category is not null)
        query = query.Where(x => x.Classification != null && x.Classification.Category == category);

    if (jobRelatedOnly == true)
        query = query.Where(x => x.Classification != null && x.Classification.IsJobRelated);

    int total = query.Count();

    var items = query
        .OrderByDescending(x => x.Email.ReceivedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(x => new
        {
            messageId = x.Email.MessageId,
            from = x.Email.FromAddress,
            subject = x.Email.Subject,
            receivedAt = x.Email.ReceivedAt,
            isJobRelated = x.Classification != null && x.Classification.IsJobRelated,
            category = x.Classification != null ? x.Classification.Category : (string?)null,
            company = x.Classification != null ? x.Classification.Company : null,
            roleTitle = x.Classification != null ? x.Classification.RoleTitle : null,
            confidence = x.Classification != null ? x.Classification.Confidence : (double?)null,
        })
        .ToList();

    return Results.Ok(new { items, total, page, pageSize });
});

app.Run();

// ---------------------------------------------------------------------------
static string ResolveDbPath(string _)
{
    const string dbFile = "job_search.db";

    // Find repo root by walking up from the exe until we find .gitignore
    string? repoRoot = null;
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, ".gitignore")))
        {
            repoRoot = dir.FullName;
            break;
        }
        dir = dir.Parent;
    }

    if (repoRoot is not null)
    {
        // Prefer existing file at repo root, then legacy location inside JobSearchAgent/
        string atRoot  = Path.Combine(repoRoot, dbFile);
        string atAgent = Path.Combine(repoRoot, "JobSearchAgent", dbFile);
        if (File.Exists(atRoot))  { Console.WriteLine($"[DB] {atRoot}");  return atRoot; }
        if (File.Exists(atAgent)) { Console.WriteLine($"[DB] {atAgent}"); return atAgent; }
        Console.WriteLine($"[DB] {atRoot} (new)");
        return atRoot;
    }

    // Fallback: CWD
    string fallback = Path.Combine(Directory.GetCurrentDirectory(), dbFile);
    Console.WriteLine($"[DB] {fallback} (fallback)");
    return fallback;
}

// ---------------------------------------------------------------------------
// Minimal read-only DbContext — shares the same schema as JobSearchAgent
// ---------------------------------------------------------------------------
class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<RawEmailRecord> RawEmails { get; set; }
    public DbSet<ClassificationRecord> Classifications { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<RawEmailRecord>().HasIndex(e => e.MessageId).IsUnique();
        builder.Entity<ClassificationRecord>().HasIndex(c => c.MessageId).IsUnique();
    }
}

class RawEmailRecord
{
    public int Id { get; set; }
    public string MessageId { get; set; } = "";
    public string ThreadId { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string Subject { get; set; } = "";
    public string BodyText { get; set; } = "";
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

class ClassificationRecord
{
    public int Id { get; set; }
    public string MessageId { get; set; } = "";
    public bool IsJobRelated { get; set; }
    public string Category { get; set; } = "";
    public double Confidence { get; set; }
    public string Company { get; set; } = "";
    public string RoleTitle { get; set; } = "";
    public DateTime ClassifiedAt { get; set; }
}
