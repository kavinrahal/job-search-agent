using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(AppDbContext.GetConnectionString(
        builder.Configuration.GetConnectionString("DefaultConnection"))));

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:5173", "http://localhost:3000")
     .AllowAnyHeader()
     .AllowAnyMethod()));

var app = builder.Build();
app.UseCors();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
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

    var appsByStatus = db.Applications
        .GroupBy(a => a.Status)
        .Select(g => new { Status = g.Key, Count = g.Count() })
        .ToList();

    return Results.Ok(new
    {
        total = db.RawEmails.Count(),
        classified = db.Classifications.Count(),
        jobRelated = db.Classifications.Count(c => c.IsJobRelated),
        byCategory = categories.ToDictionary(x => x.Category, x => x.Count),
        applications = new
        {
            total = db.Applications.Count(),
            byStatus = appsByStatus.ToDictionary(x => x.Status, x => x.Count),
        },
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

// ---------------------------------------------------------------------------
// GET /api/applications?status=...&page=1&pageSize=25
// ---------------------------------------------------------------------------
app.MapGet("/api/applications", (
    AppDbContext db,
    string? status = null,
    int page = 1,
    int pageSize = 25) =>
{
    var query = db.Applications.AsQueryable();

    if (status is not null)
        query = query.Where(a => a.Status == status);

    int total = query.Count();

    var items = query
        .OrderByDescending(a => a.UpdatedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(a => new
        {
            id = a.Id,
            company = a.Company,
            roleTitle = a.RoleTitle,
            jobUrl = a.JobUrl,
            status = a.Status,
            appliedAt = a.AppliedAt,
            updatedAt = a.UpdatedAt,
            notes = a.Notes,
        })
        .ToList();

    return Results.Ok(new { items, total, page, pageSize });
});

// ---------------------------------------------------------------------------
// GET /api/applications/{id}/events
// ---------------------------------------------------------------------------
app.MapGet("/api/applications/{id}/events", (int id, AppDbContext db) =>
{
    var app = db.Applications.Find(id);
    if (app is null) return Results.NotFound();

    var events = db.ApplicationEvents
        .Where(e => e.ApplicationId == id)
        .OrderBy(e => e.OccurredAt)
        .Select(e => new
        {
            id = e.Id,
            eventType = e.EventType,
            fromStatus = e.FromStatus,
            toStatus = e.ToStatus,
            summary = e.Summary,
            occurredAt = e.OccurredAt,
        })
        .ToList();

    return Results.Ok(new
    {
        application = new
        {
            id = app.Id,
            company = app.Company,
            roleTitle = app.RoleTitle,
            status = app.Status,
            appliedAt = app.AppliedAt,
            updatedAt = app.UpdatedAt,
            notes = app.Notes,
        },
        events,
    });
});

// ---------------------------------------------------------------------------
// GET /api/activity?limit=20
// ---------------------------------------------------------------------------
app.MapGet("/api/activity", (AppDbContext db, int limit = 20) =>
{
    var items = db.ApplicationEvents
        .Join(db.Applications,
            e => e.ApplicationId,
            a => a.Id,
            (e, a) => new { Event = e, App = a })
        .OrderByDescending(x => x.Event.OccurredAt)
        .Take(limit)
        .Select(x => new
        {
            applicationId = x.App.Id,
            company = x.App.Company,
            roleTitle = x.App.RoleTitle,
            eventType = x.Event.EventType,
            fromStatus = x.Event.FromStatus,
            toStatus = x.Event.ToStatus,
            summary = x.Event.Summary,
            occurredAt = x.Event.OccurredAt,
        })
        .ToList();

    return Results.Ok(items);
});

// ---------------------------------------------------------------------------
// GET /api/health  — dead man's switch for UptimeRobot
// ---------------------------------------------------------------------------
app.MapGet("/api/health", (AppDbContext db) =>
{
    var last = db.SystemHealth
        .OrderByDescending(h => h.CheckedAt)
        .FirstOrDefault();

    var now = DateTime.UtcNow;
    string status;
    double? ageMinutes = null;

    if (last is null)
    {
        status = "unknown";
    }
    else
    {
        ageMinutes = (now - last.CheckedAt).TotalMinutes;
        status = ageMinutes <= 20 ? "ok" : "stale";
    }

    var result = new
    {
        status,
        lastRunAt = last?.CheckedAt,
        lastRunAgeMinutes = ageMinutes.HasValue ? Math.Round(ageMinutes.Value, 1) : (double?)null,
        emailsFetched = last?.EmailsFetched,
        emailsClassified = last?.EmailsClassified,
        newApplications = last?.NewApplications,
        durationMs = last?.DurationMs,
        lastError = last?.Error,
        totalApplications = db.Applications.Count(),
        pendingNotifications = db.Notifications.Count(n => n.SentAt == null),
    };

    // Return 503 when stale so UptimeRobot triggers an alert
    return status == "stale"
        ? Results.Json(result, statusCode: 503)
        : Results.Ok(result);
});

app.Run();
