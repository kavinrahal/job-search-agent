using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using JobSearch.Api.Services;
using JobSearch.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.HttpOverrides;
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

#pragma warning disable S1135 // TODO(whatsapp): pilot paused — blocked on Meta Business Verification
// (Account Restricted). Fully built and safe to leave as-is: every value below is
// optional and WhatsAppService.IsConfigured gates every send/receive, so this stays
// a no-op until the env vars are set. To resume: complete verification, then follow
// the remaining setup steps (System User token, App Secret, webhook subscription,
// template submission) — see project_whatsapp_pilot memory / the WhatsApp plan.
#pragma warning restore S1135
//
// WhatsApp is an optional, parallel pilot channel — unlike Telegram above, nothing
// here throws at startup. WhatsAppService.IsConfigured gates every send/receive so a
// missing or half-set-up WhatsApp integration never blocks the app or Telegram.
var whatsappAccessToken = builder.Configuration["WHATSAPP_ACCESS_TOKEN"];
var whatsappPhoneId     = builder.Configuration["WHATSAPP_PHONE_NUMBER_ID"];
var whatsappAppSecret   = builder.Configuration["WHATSAPP_APP_SECRET"];
var whatsappVerifyToken = builder.Configuration["WHATSAPP_WEBHOOK_VERIFY_TOKEN"];
var whatsappToNumber    = builder.Configuration["WHATSAPP_TO_NUMBER"];
var whatsappTemplate    = builder.Configuration["WHATSAPP_TEMPLATE_NAME"] ?? "job_search_alert";
var whatsappLang        = builder.Configuration["WHATSAPP_TEMPLATE_LANG"] ?? "en_US";

builder.Services.AddSingleton(_ => new JobPostingFetcher());
builder.Services.AddSingleton(_ => new PostingEvaluator(anthropicApiKey));
builder.Services.AddSingleton(_ => new CoverLetterAgent(anthropicApiKey));
builder.Services.AddSingleton(_ => new CvTailorAgent(anthropicApiKey));
builder.Services.AddSingleton(_ => new TelegramService(telegramBotToken, telegramWebhookSecret, telegramChatId));
builder.Services.AddSingleton(_ => new WhatsAppService(
    whatsappAccessToken, whatsappPhoneId, whatsappAppSecret, whatsappVerifyToken,
    whatsappToNumber, whatsappTemplate, whatsappLang));

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
#pragma warning disable S7039 // CSP is intentionally restrictive — 'unsafe-inline' only for styles (no nonce support in SPA build)
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
#pragma warning restore S7039
    await next();
});

// Serve the React SPA and other static files from wwwroot/.
app.UseDefaultFiles();
app.UseStaticFiles();

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

// Recognizes our own "couldn't fetch, paste the description" prompt (identified by its
// trailing "/cv <url>" or "/letter <url>" line) so a reply to it can be treated as pasted
// posting content instead of a new command. Shared by both the Telegram and WhatsApp
// webhook handlers below — TelegramService.ExtractUrl and WhatsAppService.ExtractUrl use
// the identical regex, so either works here.
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
    AppDbContext db) =>
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

    var (updateId, text, replyToText) = TelegramService.ParseUpdate(update);

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

    // Fire-and-forget: respond 200 immediately, process in background.
    // Only Singletons, value types, and pre-fetched strings are captured.
    _ = Task.Run(async () =>
    {
        try
        {
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

                if (command == "/cv")
                {
                    var cvText = await cvAgent.GenerateAsync(postingText, evalJson);
                    var pdf = JobSearch.Api.Services.PdfRenderer.RenderCv(cvText);
                    await telegram.SendDocumentAsync(pdf, "Kavin_Abeysinghe_CV.pdf");
                }
                else
                {
                    var letter = await letterAgent.GenerateAsync(postingText, evalJson);
                    await telegram.SendChunkedAsync(letter);
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

            var evaluation = await evaluator.EvaluateAsync(fetchedText, url);
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
}).AllowAnonymous();

// ---------------------------------------------------------------------------
// WhatsApp webhook — GET handshake (subscription) + POST delivery.
// Unauthenticated but verified: GET by hub.verify_token, POST by HMAC signature.
// ---------------------------------------------------------------------------
app.MapGet("/api/v1/whatsapp/webhook", (HttpRequest request, WhatsAppService whatsapp) =>
{
    var mode      = request.Query["hub.mode"].FirstOrDefault();
    var token     = request.Query["hub.verify_token"].FirstOrDefault();
    var challenge = request.Query["hub.challenge"].FirstOrDefault();

    var result = whatsapp.HandleVerification(mode, token, challenge);
    return result is not null ? Results.Text(result) : Results.Forbid();
}).AllowAnonymous();

app.MapPost("/api/v1/whatsapp/webhook", async (
    HttpRequest request,
    WhatsAppService whatsapp,
    JobPostingFetcher fetcher,
    PostingEvaluator evaluator,
    CoverLetterAgent letterAgent,
    CvTailorAgent cvAgent,
    AppDbContext db) =>
{
    if (!whatsapp.IsConfigured) return Results.Ok(); // never wired up — drop silently

    using var ms = new MemoryStream();
    await request.Body.CopyToAsync(ms);
    var rawBody = ms.ToArray();

    var sigHeader = request.Headers["X-Hub-Signature-256"].FirstOrDefault();
    if (!whatsapp.VerifySignature(rawBody, sigHeader))
        return Results.Unauthorized();

    JsonElement body;
    try
    {
        body = JsonSerializer.Deserialize<JsonElement>(rawBody);
    }
    catch
    {
        return Results.Ok();
    }

    var update = WhatsAppService.ParseIncoming(body);
    if (update is null || update.IsStatusUpdate) return Results.Ok(); // delivery/read receipts — ignore

    if (!whatsapp.TryMarkProcessed(update.MessageId))
        return Results.Ok();

    var text = update.Text;
    if (string.IsNullOrWhiteSpace(text))
        return Results.Ok();

    var parts = text.Trim().Split(' ', 2, StringSplitOptions.TrimEntries);
    var command = parts[0].ToLowerInvariant();
    var commandArg = parts.Length > 1 ? parts[1] : null;

    // DB context is scoped — resolve and capture strings before the fire-and-forget Task.Run.
    string? storedEvalJson = null;
    string? storedTitle = null;
    string? resolvedUrl = null;
    string? repliedNotificationMessage = null;
    string? pastedPostingText = null;

    // A reply to our own "couldn't fetch, paste the description" prompt — treat this
    // whole message as the posting content for the command/URL that prompt was about,
    // bypassing fetch and DB lookups entirely (there's nothing stored, that's why the
    // prompt was sent in the first place). WhatsApp only gives us context.id, not the
    // replied-to text, so this is resolved via the in-memory mapping instead.
    if (whatsapp.TryGetPasteFallback(update.ContextId, out var pasteFallback))
    {
        command = pasteFallback.Command;
        resolvedUrl = pasteFallback.Url;
        pastedPostingText = text;
    }
    else if (command is "/cv" or "/letter")
    {
        resolvedUrl = commandArg is not null ? WhatsAppService.ExtractUrl(commandArg) : null;

        if (resolvedUrl is not null)
        {
            var posting = await db.DiscoveredPostings
                .Where(d => d.Url == resolvedUrl)
                .Select(d => new { d.EvaluationJson, d.Title })
                .FirstOrDefaultAsync();
            storedEvalJson = posting?.EvaluationJson;
            storedTitle    = posting?.Title;
        }
        else if (update.ContextId is not null)
        {
            // No URL in the command itself — resolve via the message being replied to.
            var posting = await db.DiscoveredPostings
                .Where(d => d.WhatsAppMessageId == update.ContextId)
                .Select(d => new { d.Url, d.EvaluationJson, d.Title })
                .FirstOrDefaultAsync();
            if (posting is not null)
            {
                resolvedUrl    = posting.Url;
                storedEvalJson = posting.EvaluationJson;
                storedTitle    = posting.Title;
            }
        }
    }
    else if (update.ContextId is not null)
    {
        // Bare reply to a teaser (no /cv or /letter) — resolve the full breakdown to send back.
        var posting = await db.DiscoveredPostings
            .Where(d => d.WhatsAppMessageId == update.ContextId)
            .Select(d => new { d.EvaluationJson })
            .FirstOrDefaultAsync();

        if (posting?.EvaluationJson is not null)
        {
            storedEvalJson = posting.EvaluationJson;
        }
        else
        {
            var notif = await db.Notifications
                .Where(n => n.WhatsAppMessageId == update.ContextId)
                .Select(n => new { n.Message })
                .FirstOrDefaultAsync();
            repliedNotificationMessage = notif?.Message;
        }
    }

    // Fire-and-forget: respond 200 immediately, process in background.
    // Only Singletons, value types, and pre-fetched strings are captured.
    _ = Task.Run(async () =>
    {
        try
        {
            if (command is "/cv" or "/letter")
            {
                if (resolvedUrl is null)
                {
                    await whatsapp.SendTextAsync(
                        "Please include a job URL or reply to a job notification.\n" +
                        "Example: /cv https://au.seek.com/job/12345");
                    return;
                }

                if (storedTitle is not null &&
                    storedTitle.Contains("Senior", StringComparison.OrdinalIgnoreCase))
                {
                    await whatsapp.SendTextAsync(
                        $"Skipped — this posting is Senior-level ({storedTitle}). " +
                        "No CV or cover letter generated.");
                    return;
                }

                string label = command == "/cv" ? "CV" : "cover letter";

                string? postingText;
                if (pastedPostingText is not null)
                {
                    postingText = pastedPostingText;
                    await whatsapp.SendTextAsync($"Generating {label} from what you provided...");
                }
                else
                {
                    await whatsapp.SendTextAsync($"Generating {label} for {resolvedUrl}...");

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
                        var promptWamid = await whatsapp.SendTextAsync(
                            "Couldn't fetch that posting — it may have expired or been taken down, " +
                            "and I don't have a cached copy of it either. Reply to this message with " +
                            "the job description text and I'll generate it from that instead.");
                        if (promptWamid is not null)
                            whatsapp.RememberPasteFallback(promptWamid, command, resolvedUrl);
                        return;
                    }
                }

                string evalJson = storedEvalJson ?? "{}";

                if (command == "/cv")
                {
                    var cvText = await cvAgent.GenerateAsync(postingText, evalJson);
                    var pdf = JobSearch.Api.Services.PdfRenderer.RenderCv(cvText);
                    await whatsapp.SendDocumentAsync(pdf, "Kavin_Abeysinghe_CV.pdf");
                }
                else
                {
                    var letter = await letterAgent.GenerateAsync(postingText, evalJson);
                    await whatsapp.SendChunkedAsync(letter);
                }
                return;
            }

            // Reply (no command) to a previously sent teaser → send the full breakdown.
            if (storedEvalJson is not null)
            {
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var ev = JsonSerializer.Deserialize<PostingEvaluation>(storedEvalJson, opts);
                if (ev is not null)
                {
                    await whatsapp.SendChunkedAsync(EvalFormatter.ToWhatsApp(EvalFormatter.Format(ev)));
                    return;
                }
            }
            if (repliedNotificationMessage is not null)
            {
                await whatsapp.SendTextAsync(repliedNotificationMessage);
                return;
            }

            // Default: bare URL → evaluate the posting.
            var url = WhatsAppService.ExtractUrl(text);
            if (url is null)
            {
                await whatsapp.SendTextAsync(
                    "Commands:\n" +
                    "- Send a job URL to evaluate a posting\n" +
                    "- /cv <url> — tailored CV\n" +
                    "- /letter <url> — cover letter\n" +
                    "Or reply to a job notification to get the full breakdown, or with /cv or /letter.");
                return;
            }

            await whatsapp.SendTextAsync($"Fetching {url}...");

            string fetchedText;
            try
            {
                fetchedText = await fetcher.FetchAsync(url);
            }
            catch (Exception ex)
            {
                await whatsapp.SendTextAsync($"Could not fetch that URL: {ex.Message}");
                return;
            }

            await whatsapp.SendTextAsync("Evaluating posting...");

            var evaluation = await evaluator.EvaluateAsync(fetchedText, url);
            await whatsapp.SendChunkedAsync(EvalFormatter.ToWhatsApp(EvalFormatter.Format(evaluation)));
        }
        catch (Exception ex)
        {
#pragma warning disable S2486, S108 // swallow intentionally — don't let a WhatsApp send failure mask the original error
            try { await whatsapp.SendTextAsync($"Unexpected error: {ex.Message}"); } catch { }
#pragma warning restore S2486, S108
        }
    });

    return Results.Ok();
}).AllowAnonymous();

// Unknown /api/* paths → 404 (prevents SPA fallback returning index.html for bad API calls)
app.Map("/api/{**rest}", () => Results.NotFound());

// SPA fallback — serves index.html for all non-API, non-asset paths (React Router routes)
app.MapFallbackToFile("index.html");

Console.WriteLine("[Startup] Calling app.RunAsync()...");
await app.RunAsync();
