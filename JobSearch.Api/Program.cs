using System.Security.Claims;
using System.Text.Json;
using JobSearch.Api.Services;
using JobSearch.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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

// ---------------------------------------------------------------------------
// Job search services
// ---------------------------------------------------------------------------
var anthropicApiKey = builder.Configuration["ANTHROPIC_API_KEY"]
    ?? throw new InvalidOperationException("ANTHROPIC_API_KEY not set");

var telegramBotToken = builder.Configuration["TELEGRAM_BOT_TOKEN"]
    ?? throw new InvalidOperationException("TELEGRAM_BOT_TOKEN not set");
var telegramWebhookSecret = builder.Configuration["TELEGRAM_WEBHOOK_SECRET"]
    ?? throw new InvalidOperationException("TELEGRAM_WEBHOOK_SECRET not set");
var telegramChatId = builder.Configuration["TELEGRAM_CHAT_ID"]
    ?? throw new InvalidOperationException("TELEGRAM_CHAT_ID not set");

builder.Services.AddSingleton(_ => new JobPostingFetcher());
builder.Services.AddSingleton(_ => new PostingEvaluator(anthropicApiKey));
builder.Services.AddSingleton(_ => new TelegramService(telegramBotToken, telegramWebhookSecret, telegramChatId));

// Trust X-Forwarded-Proto from Railway's load balancer regardless of its IP.
// KnownNetworks/KnownProxies must be cleared — the default (loopback-only) blocks
// cloud proxy headers, causing OAuth redirect_uris to be built with http://.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

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

// Must be first — rewrites scheme/IP from X-Forwarded-* before anything reads them.
app.UseForwardedHeaders();

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

// GET /api/v1/discoveries
api.MapGet("/discoveries", (AppDbContext db, string? recommendation = null, int page = 1, int pageSize = 25) =>
{
    var validRecs = new HashSet<string> { "strong_match", "good_match", "weak_match", "discard" };
    if (recommendation is not null && !validRecs.Contains(recommendation))
        return Results.BadRequest(new { error = "Invalid recommendation value" });

    var query = db.DiscoveredPostings.AsQueryable();

    query = recommendation is not null
        ? query.Where(d => d.Recommendation == recommendation)
        : query.Where(d => d.Recommendation != null && d.Recommendation != "error");

    int total = query.Count();

    var raw = query
        .OrderByDescending(d => d.DiscoveredAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToList();

    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var items = raw.Select(d =>
    {
        PostingEvaluation? ev = d.EvaluationJson is not null
            ? JsonSerializer.Deserialize<PostingEvaluation>(d.EvaluationJson, opts)
            : null;
        return new
        {
            id                   = d.Id,
            url                  = d.Url,
            source               = d.Source,
            title                = d.Title,
            company              = d.Company,
            recommendation       = d.Recommendation,
            disqualifierHit      = d.DisqualifierHit,
            discoveredAt         = d.DiscoveredAt,
            evaluatedAt          = d.EvaluatedAt,
            notificationSent     = d.NotificationSent,
            locationMatch        = ev?.LocationMatch,
            locationDetail       = ev?.LocationDetail,
            experienceMatch      = ev?.ExperienceMatch,
            experienceDetail     = ev?.ExperienceDetail,
            backendMatch         = ev?.BackendMatch,
            backendTechnologies  = ev?.BackendTechnologies ?? Array.Empty<string>(),
            frontendMatch        = ev?.FrontendMatch,
            salaryAssessment     = ev?.SalaryAssessment,
            salaryDetail         = ev?.SalaryDetail,
            companyAssessment    = ev?.CompanyAssessment,
            roleTypeMatch        = ev?.RoleTypeMatch,
            orangeFlags          = ev?.OrangeFlags ?? Array.Empty<string>(),
            rationale            = ev?.Rationale,
        };
    }).ToList();

    return Results.Ok(new { items, total, page, pageSize });
});

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

// ---------------------------------------------------------------------------
// Telegram webhook — unauthenticated but verified by secret token
// ---------------------------------------------------------------------------
app.MapPost("/api/v1/telegram/webhook", async (
    HttpRequest request,
    TelegramService telegram,
    JobPostingFetcher fetcher,
    PostingEvaluator evaluator) =>
{
    var secretHeader = request.Headers["X-Telegram-Bot-Api-Secret-Token"].FirstOrDefault() ?? "";
    if (!telegram.VerifySecretToken(secretHeader))
        return Results.Unauthorized();

    JsonElement update;
    try
    {
        update = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
    }
    catch
    {
        return Results.Ok();
    }

    var (updateId, text) = telegram.ParseUpdate(update);

    // Prevent duplicate processing — Telegram retries if we don't respond within 5 seconds.
    if (!telegram.TryMarkProcessed(updateId))
        return Results.Ok();

    // Fire-and-forget: respond 200 immediately, process in background.
    // All captured variables are Singletons or value types — no HttpContext captured.
    _ = Task.Run(async () =>
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                await telegram.SendMessageAsync("Send me a job posting URL to evaluate.");
                return;
            }

            var url = TelegramService.ExtractUrl(text);
            if (url is null)
            {
                await telegram.SendMessageAsync(
                    "No URL found in your message.\nSend me a LinkedIn, Seek, or other job posting URL.");
                return;
            }

            await telegram.SendMessageAsync($"Fetching <code>{url}</code>...");

            string postingText;
            try
            {
                postingText = await fetcher.FetchAsync(url);
            }
            catch (Exception ex)
            {
                await telegram.SendMessageAsync($"Could not fetch that URL: {ex.Message}");
                return;
            }

            await telegram.SendMessageAsync("Evaluating posting...");

            var ev = await evaluator.EvaluateAsync(postingText, url);

            await telegram.SendMessageAsync(FormatEvaluation(ev));
        }
        catch (Exception ex)
        {
            try { await telegram.SendMessageAsync($"Unexpected error: {ex.Message}"); } catch { }
        }
    });

    return Results.Ok();
}).AllowAnonymous();

static string FormatEvaluation(PostingEvaluation ev)
{
    var rec = ev.Recommendation switch
    {
        "strong_match" => "STRONG MATCH",
        "good_match"   => "GOOD MATCH",
        "weak_match"   => "WEAK MATCH",
        "discard"      => "DISCARD",
        _              => ev.Recommendation.ToUpperInvariant(),
    };

    var lines = new System.Text.StringBuilder();
    lines.AppendLine($"<b>{ev.Company} — {ev.RoleTitle}</b>");
    lines.AppendLine($"<b>Recommendation: {rec}</b>");

    if (ev.DisqualifierHit is not null)
        lines.AppendLine($"Disqualifier: {ev.DisqualifierHit}");

    lines.AppendLine();
    lines.AppendLine("<b>Dimensions:</b>");
    lines.AppendLine($"Sponsorship: {ev.SponsorshipVerdict}{(ev.SponsorshipEvidence is not null ? $" ({ev.SponsorshipEvidence})" : "")}");
    lines.AppendLine($"Location: {ev.LocationDetail} ({ev.LocationMatch})");
    lines.AppendLine($"Experience: {ev.ExperienceDetail} ({ev.ExperienceMatch})");

    var backend = ev.BackendTechnologies.Length > 0
        ? string.Join(", ", ev.BackendTechnologies)
        : "not stated";
    lines.AppendLine($"Backend: {backend} ({ev.BackendMatch})");

    var frontend = ev.FrontendTechnologies.Length > 0
        ? string.Join(", ", ev.FrontendTechnologies)
        : "not stated";
    lines.AppendLine($"Frontend: {frontend} ({ev.FrontendMatch})");

    lines.AppendLine($"Salary: {ev.SalaryDetail ?? "not stated"} ({ev.SalaryAssessment})");
    lines.AppendLine($"Company: {ev.CompanyAssessment}");
    lines.AppendLine($"Role type: {ev.RoleTypeMatch}");

    lines.AppendLine();
    if (ev.OrangeFlags.Length > 0)
    {
        lines.AppendLine("<b>Orange flags:</b>");
        foreach (var flag in ev.OrangeFlags)
            lines.AppendLine($"• {flag}");
    }
    else
    {
        lines.AppendLine("<b>Orange flags:</b> none");
    }

    lines.AppendLine();
    lines.AppendLine($"<b>Rationale:</b> {ev.Rationale}");

    if (ev.SourceUrl is not null)
        lines.AppendLine($"\n<a href=\"{ev.SourceUrl}\">View posting</a>");

    return lines.ToString().TrimEnd();
}

// Unknown /api/* paths → 404 (prevents SPA fallback returning index.html for bad API calls)
app.Map("/api/{**rest}", () => Results.NotFound());

// SPA fallback — serves index.html for all non-API, non-asset paths (React Router routes)
app.MapFallbackToFile("index.html");

app.Run();
