using System.Security.Claims;
using JobSearch.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://+:{port}");

var isDev = builder.Environment.IsDevelopment();

// ---------------------------------------------------------------------------
// Database
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(AppDbContext.GetConnectionString(
        builder.Configuration.GetConnectionString("DefaultConnection"))));

// ---------------------------------------------------------------------------
// Authentication — Google OAuth + cookie session
// ---------------------------------------------------------------------------
var allowedEmail = builder.Configuration["ALLOWED_EMAIL"] ?? "kavinrahal@gmail.com";

builder.Services.AddAuthentication(o =>
{
    o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie(o =>
{
    // __Host- prefix requires Secure + Path=/ + no Domain — strongest browser guarantee.
    // In dev we're on plain HTTP so we drop the prefix and the Secure requirement.
    o.Cookie.Name = isDev ? "session" : "__Host-session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SecurePolicy = isDev ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.ExpireTimeSpan = TimeSpan.FromDays(7);
    o.SlidingExpiration = true;
    // Return 401/403 for API callers instead of redirecting them to a login page.
    o.Events.OnRedirectToLogin = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    o.Events.OnRedirectToAccessDenied = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
})
.AddGoogle(o =>
{
    o.ClientId = builder.Configuration["GOOGLE_CLIENT_ID"]
        ?? throw new InvalidOperationException("GOOGLE_CLIENT_ID not set");
    o.ClientSecret = builder.Configuration["GOOGLE_CLIENT_SECRET"]
        ?? throw new InvalidOperationException("GOOGLE_CLIENT_SECRET not set");
    o.CallbackPath = "/api/v1/auth/callback/google";
    // Reject any Google account that isn't the owner's.
    o.Events.OnCreatingTicket = ctx =>
    {
        var email = ctx.Identity?.FindFirst(ClaimTypes.Email)?.Value;
        if (!string.Equals(email, allowedEmail, StringComparison.OrdinalIgnoreCase))
            ctx.Fail("Unauthorized email");
        return Task.CompletedTask;
    };
    o.Events.OnRemoteFailure = ctx =>
    {
        ctx.Response.Redirect("/api/v1/auth/denied");
        ctx.HandleResponse();
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization();

if (!isDev)
{
    builder.Services.AddHsts(o =>
    {
        o.MaxAge = TimeSpan.FromDays(365);
        o.IncludeSubDomains = true;
    });
}

// ---------------------------------------------------------------------------
// Pipeline
// ---------------------------------------------------------------------------
var app = builder.Build();

if (!isDev)
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Security headers on every response (including static files).
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    ctx.Response.Headers.Append("X-Frame-Options", "DENY");
    ctx.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    ctx.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=()");
    ctx.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https:; " +
        "connect-src 'self' https://accounts.google.com; " +
        "font-src 'self'; " +
        "frame-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self' https://accounts.google.com");
    await next();
});

// Serve the React SPA and other static files from wwwroot/.
app.UseDefaultFiles();
app.UseStaticFiles();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.UseAuthentication();
app.UseAuthorization();

// ---------------------------------------------------------------------------
// Auth endpoints
// ---------------------------------------------------------------------------
app.MapGet("/api/v1/auth/login", (HttpContext ctx) =>
{
    // After the OAuth round-trip, redirect back to the SPA root.
    // In dev we run the SPA on the Vite port, so redirect there instead.
    var redirectUri = isDev ? "http://localhost:5173/" : "/";
    return Results.Challenge(
        new AuthenticationProperties { RedirectUri = redirectUri },
        [GoogleDefaults.AuthenticationScheme]);
}).AllowAnonymous();

app.MapGet("/api/v1/auth/me", (HttpContext ctx) =>
{
    var email = ctx.User.FindFirst(ClaimTypes.Email)?.Value;
    return Results.Ok(new { email });
}).RequireAuthorization();

app.MapPost("/api/v1/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok();
}).RequireAuthorization();

app.MapGet("/api/v1/auth/denied", () =>
    Results.Text("Access denied. Only the account owner can access this dashboard.")
).AllowAnonymous();

// ---------------------------------------------------------------------------
// Protected data endpoints
// ---------------------------------------------------------------------------
var api = app.MapGroup("/api/v1").RequireAuthorization();

// GET /api/v1/summary
api.MapGet("/summary", (AppDbContext db) =>
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

// GET /api/v1/emails
api.MapGet("/emails", (
    AppDbContext db,
    int page = 1,
    int pageSize = 25,
    string? category = null,
    bool? jobRelatedOnly = null,
    string? from = null,
    string? to = null) =>
{
    var validCategories = new HashSet<string>
        { "job_listing", "application_update", "recruiter_outreach", "interview_invite",
          "offer", "rejection", "not_relevant", "other" };
    if (category is not null && !validCategories.Contains(category))
        return Results.BadRequest(new { error = "Invalid category" });

    var query =
        from e in db.RawEmails
        join c in db.Classifications on e.MessageId equals c.MessageId into cls
        from c in cls.DefaultIfEmpty()
        select new { Email = e, Classification = c };

    if (from is not null && DateTime.TryParse(from, out var fromDate))
        query = query.Where(x => x.Email.ReceivedAt >= DateTime.SpecifyKind(fromDate, DateTimeKind.Utc));

    if (to is not null && DateTime.TryParse(to, out var toDate))
        query = query.Where(x => x.Email.ReceivedAt <= DateTime.SpecifyKind(toDate, DateTimeKind.Utc));

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

// GET /api/v1/applications
api.MapGet("/applications", (
    AppDbContext db,
    string? status = null,
    int page = 1,
    int pageSize = 25) =>
{
    var validStatuses = new HashSet<string>
        { "Applied", "Acknowledged", "Screening", "Interviewing",
          "FinalRound", "Offer", "Rejected", "Ghosted", "Withdrawn" };
    if (status is not null && !validStatuses.Contains(status))
        return Results.BadRequest(new { error = "Invalid status" });

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

// GET /api/v1/applications/{id}/events
api.MapGet("/applications/{id}/events", (int id, AppDbContext db) =>
{
    var application = db.Applications.Find(id);
    if (application is null) return Results.NotFound();

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
            id = application.Id,
            company = application.Company,
            roleTitle = application.RoleTitle,
            status = application.Status,
            appliedAt = application.AppliedAt,
            updatedAt = application.UpdatedAt,
            notes = application.Notes,
        },
        events,
    });
});

// GET /api/v1/activity
api.MapGet("/activity", (AppDbContext db, int limit = 20) =>
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

// GET /api/v1/health  — 503 when stale so UptimeRobot can alert
api.MapGet("/health", (AppDbContext db) =>
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

    return status == "stale"
        ? Results.Json(result, statusCode: 503)
        : Results.Ok(result);
}).AllowAnonymous(); // UptimeRobot hits this without a session

// Unknown /api/* paths → 404 (prevents SPA fallback returning index.html for bad API calls)
app.Map("/api/{**rest}", () => Results.NotFound());

// SPA fallback — serves index.html for all non-API, non-asset paths (React Router routes)
app.MapFallbackToFile("index.html");

app.Run();
