using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using JobSearch.Api;
using JobSearch.Api.Services;
using JobSearch.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.UseUrls($"http://+:{port}");
// Applies to every request (webhooks included) — a native Kestrel guard against oversized
// payloads. 8MB, not 1MB, to leave headroom for resume PDF uploads (scanned/image-heavy
// resumes can run a few MB); everything else in the app sends only small JSON bodies.
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 8_000_000);

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
// Reused only to seed the owner's own account as User #1 at startup — sign-in itself is
// now open to any Google account, which creates/looks up a Users row (see OnCreatingTicket).
var ownerEmail = builder.Configuration["ALLOWED_EMAIL"] ?? "kavinrahal@gmail.com";
const string UserIdClaimType = "jobfindr:uid";

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
    // The frontend and API are separate deployments (separate origins) in production, so the
    // session cookie has to be sendable cross-site — None, not Strict/Lax. Browsers require
    // Secure for SameSite=None, which is already true in prod. Dev stays Lax: the frontend
    // and API are same-origin there via Vite's proxy, so nothing cross-site ever happens.
    o.Cookie.SameSite = isDev ? SameSiteMode.Lax : SameSiteMode.None;
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
    // Any Google account may sign in — creates or looks up a Users row, then stamps the
    // user's own id onto the session as a distinct claim type (Google already populates
    // ClaimTypes.NameIdentifier with its own "sub" claim, so this can't reuse that type).
    o.Events.OnCreatingTicket = async ctx =>
    {
        var email = ctx.Identity?.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
        {
            ctx.Fail("Google account has no email");
            return;
        }

        var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var user = await UserProvisioningService.GetOrCreateAsync(db, email);

        if (user.DeactivatedAt is not null)
        {
            ctx.Fail("Account deactivated");
            return;
        }

        // Every user needs a UserProfile row to generate against, even a blank one — this is
        // a no-op for the owner (already seeded with real content by the startup bootstrap
        // below) and creates an empty one for a real new user's first-ever login, which the
        // onboarding flow then fills in.
        await UserProfileProvisioningService.GetOrSeedAsync(db, user.Id, background: "", cvBase: "", jobCriteria: "");

        db.AnalyticsEvents.Add(new AnalyticsEvent { UserId = user.Id, EventType = AnalyticsEventType.Login, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        ctx.Identity!.AddClaim(new Claim(UserIdClaimType, user.Id.ToString(CultureInfo.InvariantCulture)));
    };
    o.Events.OnRemoteFailure = ctx =>
    {
        ctx.Response.Redirect("/api/v1/auth/denied");
        ctx.HandleResponse();
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization();

// CORS_ORIGINS is a comma-separated allowlist (e.g. the frontend's Railway URL). Credentialed
// requests (cookies) can't use a wildcard origin, so with nothing configured, cross-origin
// requests are simply rejected rather than silently falling back to allow-all.
var corsOrigins = (builder.Configuration["CORS_ORIGINS"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(policy =>
{
    if (corsOrigins.Length > 0)
        policy.WithOrigins(corsOrigins).AllowCredentials().AllowAnyHeader().AllowAnyMethod();
}));

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
builder.Services.AddSingleton(sp => new ClaudeUsageLogger(sp.GetRequiredService<DbContextOptions<AppDbContext>>()));
builder.Services.AddSingleton(sp => new PostingEvaluator(anthropicApiKey, sp.GetRequiredService<ClaudeUsageLogger>()));
builder.Services.AddSingleton(sp => new CoverLetterAgent(anthropicApiKey, sp.GetRequiredService<ClaudeUsageLogger>()));
builder.Services.AddSingleton(sp => new CvTailorAgent(anthropicApiKey, sp.GetRequiredService<ClaudeUsageLogger>()));
builder.Services.AddSingleton(sp => new AnswerAgent(anthropicApiKey, sp.GetRequiredService<ClaudeUsageLogger>()));
builder.Services.AddSingleton(sp => new ResumeIntakeAgent(anthropicApiKey, sp.GetRequiredService<ClaudeUsageLogger>()));
builder.Services.AddSingleton(_ => new TelegramService(telegramBotToken, telegramWebhookSecret, telegramChatId));

// Trust X-Forwarded-Proto from Railway's load balancer regardless of its IP.
// KnownNetworks/KnownProxies must be cleared — the default (loopback-only) blocks
// cloud proxy headers, causing OAuth redirect_uris to be built with http://.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
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
// Rate limiting — public webhook and cost-incurring generation endpoints
// ---------------------------------------------------------------------------
builder.Services.AddRateLimiter(o =>
{
    o.OnRejected = (ctx, _) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        return ValueTask.CompletedTask;
    };

    // Telegram webhook: unauthenticated (secret-token verified), internet-facing.
    o.AddFixedWindowLimiter("webhook", w =>
    {
        w.PermitLimit = 60;
        w.Window = TimeSpan.FromMinutes(1);
    });

    // Generation endpoints: credits already cap cost, this stops one signed-in user's
    // client from retry-looping or scripted abuse. Partitioned per user, not per IP.
    o.AddPolicy("generation", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.User.FindFirstValue(UserIdClaimType) ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

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
    await next();
});

app.UseCors();

Console.WriteLine("[Startup] Running database migration...");
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    Console.WriteLine("[Startup] Migration complete.");
}
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"[Startup] Migration failed: {ex}");
    throw;
}

// Seed the owner's own account as User #1 — permanent personal testing account with
// full Tier 1 + Tier 2 access, no paywall, separate from the beta cohort. Captured here
// because the Telegram webhook has no logged-in session to derive a tenant from — it acts
// as this owner for every DB read/write (see CurrentUserId usages below).
int ownerUserId;
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var owner = await UserProvisioningService.GetOrCreateAsync(db, ownerEmail, UserTier.Tier2, 1_000_000);
    ownerUserId = owner.Id;
    db.CurrentUserId = ownerUserId;

    // Seed the owner's UserProfile (background/CV base/job criteria) from the same
    // context/*.{yaml,md} files the agent classes used to load once at construction —
    // this is the bridge from the old single-shared-file world into per-user data.
    await UserProfileProvisioningService.GetOrSeedAsync(db, ownerUserId,
        background: SkillLoader.Load("context/background.yaml"),
        cvBase: SkillLoader.Load("context/cv_base.md"),
        jobCriteria: SkillLoader.Load("context/job_criteria.yaml"));

    Console.WriteLine($"[Startup] Owner account ready: {ownerEmail}");
}

app.UseAuthentication();
app.UseAuthorization();

// Stamp the authenticated user's id onto their scoped AppDbContext so every tenant-scoped
// query/write in this request is automatically bounded to their own data (see
// AppDbContext.CurrentUserId and the HasQueryFilter calls in OnModelCreating).
app.Use(async (ctx, next) =>
{
    var claim = ctx.User.FindFirstValue(UserIdClaimType);
    if (claim is not null)
    {
        var db = ctx.RequestServices.GetRequiredService<AppDbContext>();
        db.CurrentUserId = int.Parse(claim, CultureInfo.InvariantCulture);
    }
    await next();
});

app.UseRateLimiter();

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

app.MapGet("/api/v1/auth/me", async (HttpContext ctx, AppDbContext db) =>
{
    var userId = int.Parse(ctx.User.FindFirstValue(UserIdClaimType)!, CultureInfo.InvariantCulture);
    var user = await db.Users.FindAsync(userId);
    if (user is null) return Results.Unauthorized();

    // A blank Background is exactly the state the login handler creates for a brand new
    // user (see UserProfileProvisioningService.GetOrSeedAsync call in OnCreatingTicket) — the
    // owner's is always seeded with real content, so this only ever flags a genuine first-timer.
    var profile = await db.UserProfiles.FindAsync(userId);
    bool needsOnboarding = string.IsNullOrEmpty(profile?.Background);

    return Results.Ok(new { user.Id, user.Email, user.Tier, user.CreditBalance, needsOnboarding });
}).RequireAuthorization();

app.MapPost("/api/v1/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok();
}).RequireAuthorization();

app.MapGet("/api/v1/auth/denied", () =>
    Results.Text("Access denied. Sign-in failed — please try again.")
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
            skillMatches         = ev?.SkillMatches ?? Array.Empty<SkillMatch>(),
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
        { "application_confirmation", "rejection", "interview_invitation", "recruiter_outreach",
          "scheduling_request", "offer", "follow_up_needed", "job_alert", "not_relevant" };
    if (category is not null && !validCategories.Contains(category))
        return Results.BadRequest(new { error = "Invalid category" });

    var query =
        from e in db.RawEmails
        join c in db.Classifications on e.MessageId equals c.MessageId into cls
        from c in cls.DefaultIfEmpty()
        select new { Email = e, Classification = c };

    if (from is not null && DateTime.TryParse(from, CultureInfo.InvariantCulture, out var fromDate))
        query = query.Where(x => x.Email.ReceivedAt >= DateTime.SpecifyKind(fromDate, DateTimeKind.Utc));

    if (to is not null && DateTime.TryParse(to, CultureInfo.InvariantCulture, out var toDate))
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
api.MapGet("/applications/{id}/events", async (int id, AppDbContext db) =>
{
    var application = await db.Applications.FindAsync(id);
    if (application is null) return Results.NotFound();

    var events = await db.ApplicationEvents
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
        .ToListAsync();

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

// GET /api/v1/admin/analytics — owner-only aggregate view: signup/login volume, tier
// breakdown, generation-tool usage, and a 7-day active-user count as a churn proxy.
// Gated by ownerUserId instead of a role system, since there is only one admin account
// today. A proper role column can replace this if a second admin is ever needed.
api.MapGet("/admin/analytics", async (HttpContext ctx, AppDbContext db) =>
{
    if (CurrentUserId(ctx, UserIdClaimType) != ownerUserId)
        return Results.Json(new { error = "Forbidden" }, statusCode: StatusCodes.Status403Forbidden);

    return Results.Ok(await AnalyticsService.GetSummaryAsync(db, DateTime.UtcNow));
});

// POST /api/v1/support — body: { message: string }. Email/UserId are taken from the
// authenticated session, not the request body, so the form itself only needs a message.
api.MapPost("/support", async (HttpContext ctx, SupportMessageRequest body, AppDbContext db) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    var user = await db.Users.FindAsync(userId);
    if (user is null) return Results.NotFound();

    db.SupportMessages.Add(new SupportMessage
    {
        UserId = userId,
        Email = user.Email,
        Message = body.Message,
        CreatedAt = DateTime.UtcNow,
    });
    await db.SaveChangesAsync();

    return Results.Ok();
});

// GET /api/v1/admin/support — owner-only, no dedicated frontend page (same as
// /admin/analytics) — recent submissions, newest first.
api.MapGet("/admin/support", async (HttpContext ctx, AppDbContext db) =>
{
    if (CurrentUserId(ctx, UserIdClaimType) != ownerUserId)
        return Results.Json(new { error = "Forbidden" }, statusCode: StatusCodes.Status403Forbidden);

    var messages = await db.SupportMessages
        .OrderByDescending(m => m.CreatedAt)
        .Take(100)
        .Select(m => new { m.Id, m.Email, m.Message, m.CreatedAt })
        .ToListAsync();

    return Results.Ok(messages);
});

// ---------------------------------------------------------------------------
// Onboarding — resume parsing, and saving the result to a profile.
// ---------------------------------------------------------------------------

// POST /api/v1/onboarding/parse-resume — multipart form: either a "text" field (pasted
// resume) or a "file" field (PDF). Returns a preview for the user to review/edit, not saved.
api.MapPost("/onboarding/parse-resume", async (
    HttpRequest request, ResumeIntakeAgent intakeAgent, HttpContext ctx) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    var form = await request.ReadFormAsync();
    var file = form.Files["file"];
    var text = form["text"].ToString();

    ParsedResume parsed;
    if (file is { Length: > 0 })
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        parsed = await intakeAgent.ParseFromPdfAsync(userId, ms.ToArray());
    }
    else if (!string.IsNullOrWhiteSpace(text))
    {
        parsed = await intakeAgent.ParseFromTextAsync(userId, text);
    }
    else
    {
        return Results.BadRequest(new { error = "Provide either a \"text\" field or a \"file\" (PDF) field." });
    }

    return Results.Ok(new { background = parsed.Background, cvBase = parsed.CvBase });
}).RequireRateLimiting("generation");

// GET /api/v1/profile
api.MapGet("/profile", async (HttpContext ctx, AppDbContext db) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    var profile = await db.UserProfiles.FindAsync(userId);
    if (profile is null) return Results.NotFound();
    return Results.Ok(new { profile.Background, profile.CvBase, profile.JobCriteria, profile.UpdatedAt });
});

// PUT /api/v1/profile — partial update: only provided fields change.
api.MapPut("/profile", async (HttpContext ctx, ProfileUpdateRequest body, AppDbContext db) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    var profile = await db.UserProfiles.FindAsync(userId);
    if (profile is null) return Results.NotFound();

    if (body.Background is not null) profile.Background = body.Background;
    if (body.CvBase is not null) profile.CvBase = body.CvBase;
    if (body.JobCriteria is not null) profile.JobCriteria = body.JobCriteria;
    profile.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Ok(new { profile.Background, profile.CvBase, profile.JobCriteria, profile.UpdatedAt });
});

// POST /api/v1/account/cancel — soft-deactivates the account (blocks future login, data is
// kept) and signs out the current session immediately. Doesn't revoke any other active
// session for this account elsewhere — there's no server-side session store to revoke
// against, only the signed cookie — so a second open tab stays signed in until it expires
// or the cookie is cleared there too. Acceptable for now: the login check still blocks any
// future sign-in attempt regardless.
api.MapPost("/account/cancel", async (HttpContext ctx, AppDbContext db) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    var user = await db.Users.FindAsync(userId);
    if (user is null) return Results.NotFound();

    user.DeactivatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    return Results.Ok();
});

// ---------------------------------------------------------------------------
// CV / cover letter / answer generation — authenticated web endpoints. Telegram is now an
// optional notification channel layered on top of this same AgentThread mechanism, not the
// only way to act (see the Telegram webhook section below, which is still its own separate
// implementation for now — unifying the two is a reasonable later cleanup, not required for
// this to work correctly).
// ---------------------------------------------------------------------------

// Resolves posting text from a pasted URL, an existing (per-user) DiscoveredPosting, or
// pasted text directly — in that priority order. Falls back to the cached evaluation summary
// if a DiscoveredPosting can't be re-fetched — same fallback Telegram uses — but unlike
// Telegram's reply-to-this-message trick, the caller just retries the same POST with
// postingText set; no stateful correlation needed.
static async Task<(string? PostingText, string EvalJson, string? Error)> ResolvePostingTextAsync(
    AppDbContext db, JobPostingFetcher fetcher, int? discoveryId, string? postingText, string? postingUrl = null)
{
    if (postingText is not null)
        return (postingText, "{}", null);

    if (postingUrl is not null)
    {
        try
        {
            return (await fetcher.FetchAsync(postingUrl), "{}", null);
        }
        catch
        {
            return (null, "{}", "Could not fetch that URL. Paste the posting text instead.");
        }
    }

    if (discoveryId is null)
        return (null, "{}", "Provide a discoveryId, postingUrl, or postingText.");

    var posting = await db.DiscoveredPostings.FindAsync(discoveryId.Value);
    if (posting is null)
        return (null, "{}", "Discovery not found.");

    string evalJson = posting.EvaluationJson ?? "{}";
    try
    {
        var text = await fetcher.FetchAsync(posting.Url);
        return (text, evalJson, null);
    }
    catch
    {
        if (posting.EvaluationJson is not null)
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var ev = JsonSerializer.Deserialize<PostingEvaluation>(posting.EvaluationJson, opts);
            if (ev is not null) return (EvalFormatter.ToPostingContext(ev), evalJson, null);
        }
        return (null, evalJson, "Could not fetch the posting and no cached copy is available. Retry with postingText.");
    }
}

static int CurrentUserId(HttpContext ctx, string claimType) =>
    int.Parse(ctx.User.FindFirstValue(claimType)!, CultureInfo.InvariantCulture);

// Shared by /cv and /letter — identical shape (resolve posting → generate → save a Complete
// thread → spend a credit → return { threadId, text }), differing only in which agent
// generates and how the initial user turn is built.
static async Task<IResult> GenerateArtifactAsync(
    AppDbContext db, JobPostingFetcher fetcher, int userId,
    int? discoveryId, string? postingText, string? postingUrl, string artifactType,
    Func<string, string, Task<string>> generate,
    Func<string, string, string> buildInitialUserContent)
{
    var (resolvedText, evalJson, error) = await ResolvePostingTextAsync(db, fetcher, discoveryId, postingText, postingUrl);
    if (resolvedText is null)
        return Results.BadRequest(new { error });

    var text = await generate(resolvedText, evalJson);

    var thread = new AgentThread
    {
        UserId = userId,
        ArtifactType = artifactType,
        HistoryJson = JsonSerializer.Serialize(new List<AgentThreadTurn>
        {
            new("user", buildInitialUserContent(resolvedText, evalJson)),
            new("assistant", text),
        }),
        CurrentContent = text,
        Status = AgentThreadStatus.Complete,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };
    db.AgentThreads.Add(thread);
    db.AnalyticsEvents.Add(new AnalyticsEvent
    {
        UserId = userId,
        EventType = artifactType == AgentThreadType.Cv ? AnalyticsEventType.CvGenerated : AnalyticsEventType.LetterGenerated,
        CreatedAt = DateTime.UtcNow,
    });
    await db.SaveChangesAsync();
    await CreditService.SpendCreditAsync(db, userId);

    return Results.Ok(new { threadId = thread.Id, text });
}

// POST /api/v1/cv — body: { discoveryId?: int, postingText?: string }
api.MapPost("/cv", async (HttpContext ctx, GenerateRequest body, AppDbContext db, JobPostingFetcher fetcher, CvTailorAgent cvAgent) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    if (!await CreditService.HasCreditAsync(db, userId))
        return Results.Json(new { error = "Insufficient credits" }, statusCode: StatusCodes.Status402PaymentRequired);

    var profile = await db.UserProfiles.FindAsync(userId)
        ?? throw new InvalidOperationException("UserProfile not found for the current user.");

    return await GenerateArtifactAsync(db, fetcher, userId, body.DiscoveryId, body.PostingText, body.PostingUrl,
        AgentThreadType.Cv,
        (text, evalJson) => cvAgent.GenerateAsync(profile, text, evalJson),
        CvTailorAgent.BuildInitialUserContent);
}).RequireRateLimiting("generation");

// POST /api/v1/letter — body: { discoveryId?: int, postingText?: string }
api.MapPost("/letter", async (HttpContext ctx, GenerateRequest body, AppDbContext db, JobPostingFetcher fetcher, CoverLetterAgent letterAgent) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    if (!await CreditService.HasCreditAsync(db, userId))
        return Results.Json(new { error = "Insufficient credits" }, statusCode: StatusCodes.Status402PaymentRequired);

    var profile = await db.UserProfiles.FindAsync(userId)
        ?? throw new InvalidOperationException("UserProfile not found for the current user.");

    return await GenerateArtifactAsync(db, fetcher, userId, body.DiscoveryId, body.PostingText, body.PostingUrl,
        AgentThreadType.CoverLetter,
        (text, evalJson) => letterAgent.GenerateAsync(profile, text, evalJson),
        CoverLetterAgent.BuildInitialUserContent);
}).RequireRateLimiting("generation");

// POST /api/v1/answer — body: { question: string, discoveryId?: int, postingUrl?: string }
api.MapPost("/answer", async (HttpContext ctx, AnswerRequest body, AppDbContext db, JobPostingFetcher fetcher, AnswerAgent answerAgent) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    if (!await CreditService.HasCreditAsync(db, userId))
        return Results.Json(new { error = "Insufficient credits" }, statusCode: StatusCodes.Status402PaymentRequired);

    string? jobContext = null;
    if (body.PostingUrl is not null)
    {
        try { jobContext = await fetcher.FetchAsync(body.PostingUrl); }
        catch { /* no job context is fine — the question can still be answered generically */ }
    }
    else if (body.DiscoveryId is int discoveryId)
    {
        var posting = await db.DiscoveredPostings.FindAsync(discoveryId);
        if (posting?.EvaluationJson is not null)
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var ev = JsonSerializer.Deserialize<PostingEvaluation>(posting.EvaluationJson, opts);
            if (ev is not null) jobContext = EvalFormatter.ToPostingContext(ev);
        }
    }

    var profile = await db.UserProfiles.FindAsync(userId)
        ?? throw new InvalidOperationException("UserProfile not found for the current user.");

    var history = new List<AgentThreadTurn> { new("user", AnswerAgent.BuildInitialUserContent(body.Question, jobContext)) };
    var (mode, content) = await answerAgent.RespondAsync(profile, history);
    history.Add(new AgentThreadTurn("assistant", content));

    var thread = new AgentThread
    {
        UserId = userId,
        ArtifactType = AgentThreadType.Answer,
        HistoryJson = JsonSerializer.Serialize(history),
        CurrentContent = mode == "final_answer" ? content : null,
        Status = mode == "final_answer" ? AgentThreadStatus.Complete : AgentThreadStatus.AwaitingContext,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };
    db.AgentThreads.Add(thread);
    db.AnalyticsEvents.Add(new AnalyticsEvent { UserId = userId, EventType = AnalyticsEventType.AnswerGenerated, CreatedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();
    await CreditService.SpendCreditAsync(db, userId);

    return Results.Ok(new { threadId = thread.Id, mode, content });
}).RequireRateLimiting("generation");

// POST /api/v1/threads/{id}/edit — body: { message: string }
// Dual-purpose, same as Telegram's /edit: on an AwaitingContext (Answer) thread this
// continues the Q&A; on a Complete thread it's a revision request.
api.MapPost("/threads/{id:int}/edit", async (
    int id, HttpContext ctx, EditRequest body, AppDbContext db,
    CvTailorAgent cvAgent, CoverLetterAgent letterAgent, AnswerAgent answerAgent) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    var thread = await db.AgentThreads.FindAsync(id);
    if (thread is null) return Results.NotFound();

    if (!await CreditService.HasCreditAsync(db, userId))
        return Results.Json(new { error = "Insufficient credits" }, statusCode: StatusCodes.Status402PaymentRequired);

    var profile = await db.UserProfiles.FindAsync(userId)
        ?? throw new InvalidOperationException("UserProfile not found for the current user.");
    var history = JsonSerializer.Deserialize<List<AgentThreadTurn>>(thread.HistoryJson) ?? [];

    if (thread.Status == AgentThreadStatus.AwaitingContext)
    {
        var followupRounds = history.Count(t => t.Role == "assistant");
        var userTurn = followupRounds >= 3
            ? $"{body.Message}\n\n(Please give your best answer now instead of asking another question.)"
            : body.Message;
        history.Add(new AgentThreadTurn("user", userTurn));

        var (mode, content) = await answerAgent.RespondAsync(profile, history);
        history.Add(new AgentThreadTurn("assistant", content));

        thread.HistoryJson = JsonSerializer.Serialize(history);
        thread.CurrentContent = mode == "final_answer" ? content : null;
        thread.Status = mode == "final_answer" ? AgentThreadStatus.Complete : AgentThreadStatus.AwaitingContext;
        thread.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await CreditService.SpendCreditAsync(db, userId);

        return Results.Ok(new { threadId = thread.Id, mode, content });
    }

    history.Add(new AgentThreadTurn("user",
        $"Please revise the previous draft with this feedback: {body.Message}\n\n" +
        "Keep following all the rules in your instructions unless the feedback specifically asks to change them."));

    string? text = null;
    string? answerMode = null;
    string? answerContent = null;

    if (thread.ArtifactType == AgentThreadType.Cv)
        text = await cvAgent.ReviseAsync(profile, history);
    else if (thread.ArtifactType == AgentThreadType.CoverLetter)
        text = await letterAgent.ReviseAsync(profile, history);
    else
        (answerMode, answerContent) = await answerAgent.RespondAsync(profile, history);

    history.Add(new AgentThreadTurn("assistant", text ?? answerContent ?? ""));
    thread.HistoryJson = JsonSerializer.Serialize(history);
    thread.CurrentContent = text ?? answerContent;
    thread.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    await CreditService.SpendCreditAsync(db, userId);

    return Results.Ok(new { threadId = thread.Id, text, mode = answerMode, content = answerContent });
}).RequireRateLimiting("generation");

// GET /api/v1/threads/{id}/pdf — renders a completed CV thread's content as a downloadable PDF
api.MapGet("/threads/{id:int}/pdf", async (int id, AppDbContext db) =>
{
    var thread = await db.AgentThreads.FindAsync(id);
    if (thread is null || thread.ArtifactType != AgentThreadType.Cv || thread.CurrentContent is null)
        return Results.NotFound();

    var pdf = JobSearch.Api.Services.PdfRenderer.RenderCv(thread.CurrentContent);
    return Results.File(pdf, "application/pdf", "CV.pdf");
});

// Recognizes our own "couldn't fetch, paste the description" prompt (identified by its
// trailing "/cv <url>" or "/letter <url>" line) so a reply to it can be treated as pasted
// posting content instead of a new command.
static (string Command, string Url)? ParsePasteFallbackPrompt(string promptText)
{
    var lastLine = promptText.TrimEnd().Split('\n').LastOrDefault()?.Trim();
    if (string.IsNullOrEmpty(lastLine)) return null;

    var parts = lastLine.Split(' ', 2, StringSplitOptions.TrimEntries);
    if (parts.Length != 2) return null;

    var cmd = parts[0].ToLowerInvariant();
    if (cmd is not ("/cv" or "/letter")) return null;

    var url = TelegramService.ExtractUrl(parts[1]);
    return url is not null ? (cmd, url) : null;
}

// ---------------------------------------------------------------------------
// Telegram webhook — unauthenticated but verified by secret token
// ---------------------------------------------------------------------------
app.MapPost("/api/v1/telegram/webhook", async (
    HttpRequest request,
    TelegramService telegram,
    JobPostingFetcher fetcher,
    PostingEvaluator evaluator,
    CoverLetterAgent letterAgent,
    CvTailorAgent cvAgent,
    AnswerAgent answerAgent,
    AppDbContext db,
    IServiceScopeFactory scopeFactory) =>
{
    var secretHeader = request.Headers["X-Telegram-Bot-Api-Secret-Token"].FirstOrDefault() ?? "";
    if (!telegram.VerifySecretToken(secretHeader))
        return Results.Unauthorized();

    // No logged-in session on a bot webhook — Telegram stays personal-use-only, so it
    // always acts as the owner.
    db.CurrentUserId = ownerUserId;

    // Captured as plain strings (not the tracked entity) so they're safe to use after the
    // request-scoped db is gone, inside the fire-and-forget Task.Run below.
    var ownerProfileEntity = await db.UserProfiles.FindAsync(ownerUserId)
        ?? throw new InvalidOperationException("Owner UserProfile not seeded — startup seeding should have created it.");
    var ownerBackground = ownerProfileEntity.Background;
    var ownerCvBase = ownerProfileEntity.CvBase;
    var ownerJobCriteria = ownerProfileEntity.JobCriteria;

    JsonElement update;
    try
    {
        update = await JsonSerializer.DeserializeAsync<JsonElement>(request.Body);
    }
    catch
    {
        return Results.Ok();
    }

    var (updateId, text, replyToText, replyToMessageId) = TelegramService.ParseUpdate(update);

    // Prevent duplicate processing — Telegram retries if we don't respond within 5 seconds.
    if (!telegram.TryMarkProcessed(updateId))
        return Results.Ok();

    if (string.IsNullOrWhiteSpace(text))
        return Results.Ok();

    // Parse command: "/cv", "/letter", or bare URL (existing eval flow).
    // Strip @botname suffix that Telegram appends in group chats.
    var parts = text.Trim().Split(' ', 2, StringSplitOptions.TrimEntries);
    var command = parts[0].ToLowerInvariant().Split('@')[0];
    var commandArg = parts.Length > 1 ? parts[1] : null;

    // For /cv and /letter commands, look up stored evaluation before going async.
    // DB context is scoped — capture the string values we need, not the context itself.
    string? storedEvalJson = null;
    string? storedTitle = null;
    string? resolvedUrl = null;
    string? pastedPostingText = null;

    // A reply to our own "couldn't fetch, paste the description" prompt — treat this
    // whole message as the posting content for the command/URL embedded in that prompt,
    // bypassing fetch and the DB lookup entirely (there's nothing stored, that's why
    // the prompt was sent in the first place).
    var pasteFallback = replyToText is not null ? ParsePasteFallbackPrompt(replyToText) : null;
    if (pasteFallback is not null)
    {
        command = pasteFallback.Value.Command;
        resolvedUrl = pasteFallback.Value.Url;
        pastedPostingText = text;
    }
    else if (command is "/cv" or "/letter")
    {
        resolvedUrl = (commandArg is not null ? TelegramService.ExtractUrl(commandArg) : null)
            ?? (replyToText is not null ? TelegramService.ExtractUrl(replyToText) : null);

        if (resolvedUrl is not null)
        {
            var posting = await db.DiscoveredPostings
                .Where(d => d.Url == resolvedUrl)
                .Select(d => new { d.EvaluationJson, d.Title })
                .FirstOrDefaultAsync();

            storedEvalJson = posting?.EvaluationJson;
            storedTitle    = posting?.Title;
        }
    }
    else if (command == "/answer" && replyToText is not null)
    {
        // /answer has no URL of its own — commandArg is the question. Job context (if any)
        // only comes from replying to a job notification.
        var answerUrl = TelegramService.ExtractUrl(replyToText);
        if (answerUrl is not null)
        {
            storedEvalJson = await db.DiscoveredPostings
                .Where(d => d.Url == answerUrl)
                .Select(d => d.EvaluationJson)
                .FirstOrDefaultAsync();
        }
    }

    // A reply to a prior bot message that's part of an AgentThread (an open Q&A
    // conversation, or a finished CV/cover letter/answer that /edit can revise).
    int? threadId = null;
    string? threadType = null;
    string? threadStatus = null;
    string? threadHistoryJson = null;
    if (replyToMessageId is not null)
    {
        var thread = await db.AgentThreads
            .Where(t => t.LastMessageId == replyToMessageId)
            .Select(t => new { t.Id, t.ArtifactType, t.Status, t.HistoryJson })
            .FirstOrDefaultAsync();

        if (thread is not null)
        {
            threadId = thread.Id;
            threadType = thread.ArtifactType;
            threadStatus = thread.Status;
            threadHistoryJson = thread.HistoryJson;
        }
    }

    // Inserts or updates an AgentThread from inside the fire-and-forget block below, where
    // the request-scoped `db` above is no longer safe to use. No-ops if the send failed
    // (nothing to correlate a future reply to).
    async Task SaveThreadAsync(int? id, string artifactType, List<AgentThreadTurn> history,
        string? currentContent, string status, string? lastMessageId)
    {
        if (lastMessageId is null) return;

        using var scope = scopeFactory.CreateScope();
        var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        scopedDb.CurrentUserId = ownerUserId;
        var historyJson = JsonSerializer.Serialize(history);

        if (id is int existingId)
        {
            var existing = await scopedDb.AgentThreads.FindAsync(existingId);
            if (existing is not null)
            {
                existing.HistoryJson = historyJson;
                existing.CurrentContent = currentContent;
                existing.Status = status;
                existing.LastMessageId = lastMessageId;
                existing.UpdatedAt = DateTime.UtcNow;
            }
        }
        else
        {
            scopedDb.AgentThreads.Add(new AgentThread
            {
                UserId = ownerUserId,
                ArtifactType = artifactType,
                HistoryJson = historyJson,
                CurrentContent = currentContent,
                Status = status,
                LastMessageId = lastMessageId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        await scopedDb.SaveChangesAsync();
    }

    // Sends an AnswerAgent response (a clarifying question or a final answer) and saves the
    // thread accordingly. Shared by the Q&A continuation, /edit-on-answer, and new-/answer paths.
    async Task SendAnswerAsync(int? id, List<AgentThreadTurn> history, string mode, string content)
    {
        var sentId = mode == "ask_followup"
            ? await telegram.SendMessageAsync(content, parseMode: null)
            : await telegram.SendChunkedAsync(content, parseMode: null);
        await SaveThreadAsync(id, AgentThreadType.Answer, history,
            mode == "final_answer" ? content : null,
            mode == "final_answer" ? AgentThreadStatus.Complete : AgentThreadStatus.AwaitingContext,
            sentId);
    }

    // Fire-and-forget: respond 200 immediately, process in background.
    // Only Singletons, value types, and pre-fetched strings are captured.
    _ = Task.Run(async () =>
    {
        // Detached POCO built from the strings captured above — not the tracked entity,
        // which belongs to a db context that's gone by the time this runs.
        var ownerProfile = new UserProfile
        {
            UserId = ownerUserId, Background = ownerBackground, CvBase = ownerCvBase, JobCriteria = ownerJobCriteria,
        };

        try
        {
            // A reply that continues an open Q&A conversation — the whole message is the
            // candidate's answer to our clarifying question, not a new command.
            if (threadId is not null && threadStatus == AgentThreadStatus.AwaitingContext)
            {
                var history = JsonSerializer.Deserialize<List<AgentThreadTurn>>(threadHistoryJson!) ?? [];
                var followupRounds = history.Count(t => t.Role == "assistant");
                var userTurn = followupRounds >= 3
                    ? $"{text}\n\n(Please give your best answer now instead of asking another question.)"
                    : text;
                history.Add(new AgentThreadTurn("user", userTurn));

                var (mode, content) = await answerAgent.RespondAsync(ownerProfile, history);
                history.Add(new AgentThreadTurn("assistant", content));
                await SendAnswerAsync(threadId, history, mode, content);
                return;
            }

            // A reply asking to revise a previously delivered CV, cover letter, or answer.
            if (threadId is not null && threadStatus == AgentThreadStatus.Complete && command == "/edit")
            {
                if (string.IsNullOrWhiteSpace(commandArg))
                {
                    await telegram.SendMessageAsync("Reply with <code>/edit &lt;what to change&gt;</code> to revise this.");
                    return;
                }

                var history = JsonSerializer.Deserialize<List<AgentThreadTurn>>(threadHistoryJson!) ?? [];
                history.Add(new AgentThreadTurn("user",
                    $"Please revise the previous draft with this feedback: {commandArg}\n\n" +
                    "Keep following all the rules in your instructions unless the feedback specifically asks to change them."));

                if (threadType == AgentThreadType.Cv)
                {
                    var revisedCv = await cvAgent.ReviseAsync(ownerProfile, history);
                    history.Add(new AgentThreadTurn("assistant", revisedCv));
                    var pdf = JobSearch.Api.Services.PdfRenderer.RenderCv(revisedCv);
                    var sentId = await telegram.SendDocumentAsync(pdf, "Kavin_Abeysinghe_CV.pdf");
                    await SaveThreadAsync(threadId, threadType, history, revisedCv, AgentThreadStatus.Complete, sentId);
                }
                else if (threadType == AgentThreadType.CoverLetter)
                {
                    var revisedLetter = await letterAgent.ReviseAsync(ownerProfile, history);
                    history.Add(new AgentThreadTurn("assistant", revisedLetter));
                    var sentId = await telegram.SendChunkedAsync(revisedLetter);
                    await SaveThreadAsync(threadId, threadType, history, revisedLetter, AgentThreadStatus.Complete, sentId);
                }
                else
                {
                    var (mode, content) = await answerAgent.RespondAsync(ownerProfile, history);
                    history.Add(new AgentThreadTurn("assistant", content));
                    await SendAnswerAsync(threadId, history, mode, content);
                }
                return;
            }

            if (command == "/answer")
            {
                if (string.IsNullOrWhiteSpace(commandArg))
                {
                    await telegram.SendMessageAsync(
                        "Please include a question.\n" +
                        "Example: <code>/answer What made you want to apply for this role?</code>\n" +
                        "Or reply to a job notification with <code>/answer &lt;question&gt;</code> for context on that role.");
                    return;
                }

                string? jobContext = null;
                if (storedEvalJson is not null)
                {
                    try
                    {
                        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var ev = JsonSerializer.Deserialize<PostingEvaluation>(storedEvalJson, opts);
                        if (ev is not null) jobContext = EvalFormatter.ToPostingContext(ev);
                    }
                    catch (JsonException)
                    {
                        // No job context — answer from background alone.
                    }
                }

                var history = new List<AgentThreadTurn>
                {
                    new("user", AnswerAgent.BuildInitialUserContent(commandArg, jobContext)),
                };

                var (mode, content) = await answerAgent.RespondAsync(ownerProfile, history);
                history.Add(new AgentThreadTurn("assistant", content));
                await SendAnswerAsync(null, history, mode, content);
                return;
            }

            if (command is "/cv" or "/letter")
            {
                if (resolvedUrl is null)
                {
                    await telegram.SendMessageAsync(
                        "Please include a job URL or reply to a job notification.\n" +
                        "Example: <code>/cv https://au.seek.com/job/12345</code>");
                    return;
                }

                if (storedTitle is not null &&
                    storedTitle.Contains("Senior", StringComparison.OrdinalIgnoreCase))
                {
                    await telegram.SendMessageAsync(
                        $"Skipped — this posting is Senior-level ({storedTitle}). " +
                        "No CV or cover letter generated.", parseMode: null);
                    return;
                }

                string label = command == "/cv" ? "CV" : "cover letter";

                string? postingText;
                if (pastedPostingText is not null)
                {
                    postingText = pastedPostingText;
                    await telegram.SendMessageAsync($"Generating {label} from what you provided...");
                }
                else
                {
                    await telegram.SendMessageAsync(
                        $"Generating {label} for <code>{System.Net.WebUtility.HtmlEncode(resolvedUrl)}</code>...");

                    // Re-fetch the posting text. Fall back to the stored eval summary if unavailable.
                    postingText = null;
                    try
                    {
                        postingText = await fetcher.FetchAsync(resolvedUrl);
                    }
                    catch when (storedEvalJson is not null)
                    {
                        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var ev = JsonSerializer.Deserialize<PostingEvaluation>(storedEvalJson, opts);
                        if (ev is not null) postingText = EvalFormatter.ToPostingContext(ev);
                    }
                    catch
                    {
                        // No cached fallback either — postingText stays null, handled below.
                    }

                    if (postingText is null)
                    {
                        await telegram.SendMessageAsync(
                            "Couldn't fetch that posting — it may have expired or been taken down, " +
                            "and I don't have a cached copy of it either. Reply to this message with " +
                            "the job description text and I'll generate it from that instead.\n\n" +
                            $"{command} {resolvedUrl}", parseMode: null);
                        return;
                    }
                }

                string evalJson = storedEvalJson ?? "{}";
                var initialUserTurn = new AgentThreadTurn("user",
                    command == "/cv"
                        ? CvTailorAgent.BuildInitialUserContent(postingText, evalJson)
                        : CoverLetterAgent.BuildInitialUserContent(postingText, evalJson));

                if (command == "/cv")
                {
                    var cvText = await cvAgent.GenerateAsync(ownerProfile, postingText, evalJson);
                    var pdf = JobSearch.Api.Services.PdfRenderer.RenderCv(cvText);
                    var sentId = await telegram.SendDocumentAsync(pdf, "Kavin_Abeysinghe_CV.pdf");
                    await SaveThreadAsync(null, AgentThreadType.Cv,
                        [initialUserTurn, new AgentThreadTurn("assistant", cvText)],
                        cvText, AgentThreadStatus.Complete, sentId);
                }
                else
                {
                    var letter = await letterAgent.GenerateAsync(ownerProfile, postingText, evalJson);
                    var sentId = await telegram.SendChunkedAsync(letter);
                    await SaveThreadAsync(null, AgentThreadType.CoverLetter,
                        [initialUserTurn, new AgentThreadTurn("assistant", letter)],
                        letter, AgentThreadStatus.Complete, sentId);
                }
                return;
            }

            // Default: bare URL → evaluate the posting.
            var url = TelegramService.ExtractUrl(text);
            if (url is null)
            {
                await telegram.SendMessageAsync(
                    "Commands:\n" +
                    "• Send a job URL to evaluate a posting\n" +
                    "• <code>/cv &lt;url&gt;</code> — tailored CV\n" +
                    "• <code>/letter &lt;url&gt;</code> — cover letter\n" +
                    "Or reply to a job notification with <code>/cv</code> or <code>/letter</code>.");
                return;
            }

            await telegram.SendMessageAsync(
                $"Fetching <code>{System.Net.WebUtility.HtmlEncode(url)}</code>...");

            string fetchedText;
            try
            {
                fetchedText = await fetcher.FetchAsync(url);
            }
            catch (Exception ex)
            {
                await telegram.SendMessageAsync($"Could not fetch that URL: {ex.Message}", parseMode: null);
                return;
            }

            await telegram.SendMessageAsync("Evaluating posting...");

            var evaluation = await evaluator.EvaluateAsync(ownerProfile, fetchedText, url);
            await telegram.SendMessageAsync(EvalFormatter.Format(evaluation));
        }
        catch (Exception ex)
        {
#pragma warning disable S2486, S108 // swallow intentionally — don't let Telegram send failure mask the original error
            try { await telegram.SendMessageAsync($"Unexpected error: {ex.Message}", parseMode: null); } catch { }
#pragma warning restore S2486, S108
        }
    });

    return Results.Ok();
}).AllowAnonymous().RequireRateLimiting("webhook");

// Unknown /api/* paths → 404 (prevents SPA fallback returning index.html for bad API calls)
app.Map("/api/{**rest}", () => Results.NotFound());

// SPA fallback — serves index.html for all non-API, non-asset paths (React Router routes)
app.MapFallbackToFile("index.html");

Console.WriteLine("[Startup] Calling app.RunAsync()...");
await app.RunAsync();
