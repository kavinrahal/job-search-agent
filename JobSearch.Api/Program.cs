using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Google.Apis.Auth.OAuth2.Responses;
using JobSearch.Api;
using JobSearch.Api.Services;
using JobSearch.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
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

// Crash reporting. Absent DSN = disabled, which is the normal state locally and in CI —
// only the deployed service sets SENTRY_DSN. See SentryConfig for why the scrubbing is an
// allowlist: this process handles resumes and raw Gmail bodies, none of which may reach a
// third-party dashboard.
var sentryDsn = builder.Configuration["SENTRY_DSN"];
if (SentryConfig.IsEnabled(sentryDsn))
{
    builder.WebHost.UseSentry(o =>
    {
        o.Dsn = sentryDsn!;
        o.Environment = isDev ? "development" : "production";
        // Request bodies are never read for error reports — the Kestrel limit above allows
        // 8MB uploads, and a resume PDF is exactly what must not be captured.
        o.MaxRequestBodySize = Sentry.Extensibility.RequestSize.None;
        SentryConfig.Harden(o);
    });
}

// ---------------------------------------------------------------------------
// Database
// ---------------------------------------------------------------------------
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(AppDbContext.GetConnectionString(
        builder.Configuration.GetConnectionString("DefaultConnection"))));

// Key ring persisted to Postgres, not the framework's local-disk default — this API and
// JobSearchAgent are separate processes on ephemeral containers that both need to decrypt
// UserSecrets encrypted by either one. Must match JobSearchAgent's ApplicationName exactly
// (JobSearchAgent/Program.cs) or the two processes derive different keys from the same
// stored key material and neither can read the other's ciphertext.
builder.Services.AddDataProtection().PersistKeysToDbContext<AppDbContext>().SetApplicationName("JobFindr");
builder.Services.AddSingleton<UserSecretService>();

// ---------------------------------------------------------------------------
// Authentication — Google OAuth + cookie session
// ---------------------------------------------------------------------------
// Reused to seed the owner's own account as User #1 at startup, and as the Tier2-owner
// bypass in BetaAccessService — sign-in itself is gated to existing users and invited
// emails only (see BetaAccessService.ResolveSignupTierAsync); it is NOT open to any
// Google account despite what an older version of this comment used to say.
var ownerEmail = builder.Configuration["ALLOWED_EMAIL"] ?? "kavinrahal@gmail.com";
const string UserIdClaimType = "jobfindr:uid";

// Where to send the browser after the OAuth round-trip completes — the frontend's own URL,
// now that it's a separate deployment from the API and there's nothing to redirect to at
// the API's own root anymore. Only required outside dev, where the SPA runs on Vite's own
// port instead.
var frontendUrl = isDev
    ? null
    : builder.Configuration["FRONTEND_URL"] ?? throw new InvalidOperationException("FRONTEND_URL not set");

builder.Services.AddAuthentication(o =>
{
    o.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    // Challenge the cookie scheme by default, not Google directly — RequireAuthorization()
    // on every protected endpoint relies on this to hit OnRedirectToLogin below (401 for API
    // callers) instead of 302-redirecting a fetch() straight to Google's OAuth URL, which the
    // browser then blocks as a cross-origin redirect with no CORS headers. /api/v1/auth/login
    // is unaffected — it names GoogleDefaults.AuthenticationScheme explicitly.
    o.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
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
    // Runs on every authenticated request, not just at sign-in — closes the gap the
    // /account/cancel endpoint's own comment already calls out ("doesn't revoke any other
    // active session for this account elsewhere... acceptable for now"): without this, a
    // session/cookie issued before cancellation kept working fully (any endpoint, any tier)
    // for up to its full 7-day sliding expiry, since nothing but the initial Google
    // OnCreatingTicket check and the sign-in moment itself ever looked at DeactivatedAt.
    // A DB read per request is the same cost every RequireTier2Async-gated endpoint already
    // pays for its own fresh-per-request check; this is that same pattern applied to
    // cancellation instead of tier.
    o.Events.OnValidatePrincipal = async ctx =>
    {
        var uidClaim = ctx.Principal?.FindFirstValue(UserIdClaimType);
        if (uidClaim is null || !int.TryParse(uidClaim, out var uid)) return;

        var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var user = await db.Users.FindAsync(uid);
        if (user is null || user.DeactivatedAt is not null)
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    };
})
.AddGoogle(o =>
{
    o.ClientId = builder.Configuration["GOOGLE_CLIENT_ID"]
        ?? throw new InvalidOperationException("GOOGLE_CLIENT_ID not set");
    o.ClientSecret = builder.Configuration["GOOGLE_CLIENT_SECRET"]
        ?? throw new InvalidOperationException("GOOGLE_CLIENT_SECRET not set");
    o.CallbackPath = "/api/v1/auth/callback/google";
    // Same reasoning as the session cookie above (SameSite=None, Secure=Always in prod) —
    // this cookie (ASP.NET Core's own CSRF/round-trip check for the OAuth redirect to Google
    // and back) had no explicit config, so it fell back to the framework default, which is
    // stricter than what this flow actually needs. Confirmed via live logs: real invited
    // users were hitting "'.AspNetCore.Correlation.<token>' cookie not found" warnings and
    // silently landing back on the login page with no visible error — a mobile browser,
    // email-app in-app browser, or stricter cookie policy is more likely to drop a Lax
    // cookie across a redirect through accounts.google.com and back than a None one.
    o.CorrelationCookie.SameSite = isDev ? SameSiteMode.Lax : SameSiteMode.None;
    o.CorrelationCookie.SecurePolicy = isDev ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
    // Any Google account may sign in — creates or looks up a Users row, then stamps the
    // user's own id onto the session as a distinct claim type (Google already populates
    // ClaimTypes.NameIdentifier with its own "sub" claim, so this can't reuse that type).
    o.Events.OnCreatingTicket = async ctx =>
    {
        // TEMP diagnostic — investigating a live bug where fresh logins bounce back to the
        // landing page instead of authenticating. Traces exactly how far this handler gets.
        var email = ctx.Identity?.FindFirst(ClaimTypes.Email)?.Value;
        await Console.Error.WriteLineAsync($"[AuthDiag] OnCreatingTicket start, email={email}");
        if (string.IsNullOrEmpty(email))
        {
            await Console.Error.WriteLineAsync("[AuthDiag] OnCreatingTicket: failing, no email");
            ctx.Fail("Google account has no email");
            return;
        }

        var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

        // Checked before GetOrCreateAsync, not after — a rejected email must never get a
        // Users row created for it in the first place. The resolved tier only matters for a
        // brand-new account; GetOrCreateAsync ignores it for an existing one.
        var signupTier = await BetaAccessService.ResolveSignupTierAsync(db, email, ownerEmail);
        await Console.Error.WriteLineAsync($"[AuthDiag] OnCreatingTicket: signupTier={signupTier ?? "null"}");
        if (signupTier is null)
        {
            ctx.Fail("Not invited to the beta");
            return;
        }

        var user = await UserProvisioningService.GetOrCreateAsync(db, email, signupTier);
        await Console.Error.WriteLineAsync($"[AuthDiag] OnCreatingTicket: user.Id={user.Id}, deactivatedAt={user.DeactivatedAt}");

        if (user.DeactivatedAt is not null)
        {
            ctx.Fail("Account deactivated");
            return;
        }

        // Google already proves email ownership, so a successful Google login is itself a
        // verification — this is what lets a later email/password registration against the
        // same address skip re-verification (see PasswordAuthService.RegisterAsync's
        // account-linking safety net). Purely additive: doesn't change anything else about
        // this Google login itself, and never overwrites an already-set value. Persisted by
        // the SaveChangesAsync below, same as the AnalyticsEvent add right after it.
        user.EmailVerifiedAt ??= DateTime.UtcNow;

        // Every user needs a UserProfile row to generate against, even a blank one — this is
        // a no-op for the owner (already seeded with real content by the startup bootstrap
        // below) and creates an empty one for a real new user's first-ever login, which the
        // onboarding flow then fills in.
        await UserProfileProvisioningService.GetOrSeedAsync(db, user.Id, background: "", cvBase: "", jobCriteria: "");

        db.AnalyticsEvents.Add(new AnalyticsEvent { UserId = user.Id, EventType = AnalyticsEventType.Login, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        ctx.Identity!.AddClaim(new Claim(UserIdClaimType, user.Id.ToString(CultureInfo.InvariantCulture)));
        await Console.Error.WriteLineAsync($"[AuthDiag] OnCreatingTicket: added uid claim, identity claim count={ctx.Identity!.Claims.Count()}");
    };
    o.Events.OnRemoteFailure = ctx =>
    {
        // Land back on the real (styled) frontend with a query flag instead of the bare API
        // text page — a failed sign-in used to redirect to api.worksanta.com/api/v1/auth/denied,
        // which looked so different from a normal browser navigation that a genuinely-denied
        // user couldn't tell it apart from a random glitch. This way LandingPage can show them
        // an actual explanation.
        //
        // "Account deactivated" (OnCreatingTicket's own ctx.Fail message above) gets its own
        // authError value rather than folding into the generic "denied" bucket — AuthPage.tsx's
        // AUTH_ERRORS map used to have no case for it at all, so a cancelled user trying to sign
        // back in via Google saw "that Google account needs an invite", which is simply false
        // and would send them chasing an invite that was never the problem.
        var redirectBase = (frontendUrl ?? "http://localhost:5173").TrimEnd('/');
        var authError = ctx.Failure?.Message == "Account deactivated" ? "deactivated" : "denied";
        ctx.Response.Redirect($"{redirectBase}/?authError={authError}");
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

// Optional — only needed for the Seek cross-check hint fallback below; that fallback just
// skips Adzuna (Jora alone still runs) if these aren't configured.
var adzunaAppId = builder.Configuration["ADZUNA_APP_ID"];
var adzunaAppKey = builder.Configuration["ADZUNA_APP_KEY"];

// Optional, deliberately not required-and-throw like the secrets above — these depend on
// external SendGrid/DNS setup finishing first. Until both are set, the webhook always
// rejects (see below) and GET /inbound-email returns 503, rather than the whole API
// failing to start over a domain that isn't ready yet.
var sendGridInboundSecret = builder.Configuration["SENDGRID_INBOUND_SECRET"];
var sendGridInboundDomain = builder.Configuration["SENDGRID_INBOUND_DOMAIN"];

// GMAIL_CLIENT_ID/SECRET: the same Google Cloud OAuth client JobSearchAgent's existing
// single-user Gmail flow already uses — one client can request different scopes on
// different authorization requests, so this reuses it rather than provisioning a second
// one. GMAIL_OAUTH_REDIRECT_URI is this flow's own callback URL (must be added to that
// client's authorized redirect URIs in Google Cloud Console — see the Set up Google Cloud
// OAuth setup ticket). All optional, same reasoning as the SendGrid config above: external
// setup not finished yet, so /gmail-oauth/start 503s until they're all configured.
var gmailClientId = builder.Configuration["GMAIL_CLIENT_ID"];
var gmailClientSecret = builder.Configuration["GMAIL_CLIENT_SECRET"];
var gmailOAuthRedirectUri = builder.Configuration["GMAIL_OAUTH_REDIRECT_URI"];

// SendGrid Mail Send — separate key/permission from SENDGRID_INBOUND_SECRET above (that one
// only receives). Optional, same reasoning as the rest of this block: the admin invite
// endpoint still adds the BetaInvite row and reports emailSent=false if this isn't
// configured yet, rather than failing the whole invite.
var sendGridApiKey = builder.Configuration["SENDGRID_API_KEY"];
var sendGridFromEmail = builder.Configuration["SENDGRID_FROM_EMAIL"];

// Automated crash triage. SENTRY_WEBHOOK_SECRET is the Sentry Internal Integration's client
// secret (used to verify the webhook signature); CRASH_FIX_GITHUB_TOKEN is a PAT with repo +
// workflow scope, used only to fire repository_dispatch. All optional, same reasoning as the
// blocks above — the endpoint 401s until they're configured rather than failing at startup.
var sentryWebhookSecret = builder.Configuration["SENTRY_WEBHOOK_SECRET"];
var crashFixGitHubToken = builder.Configuration["CRASH_FIX_GITHUB_TOKEN"];
var crashFixRepo = builder.Configuration["CRASH_FIX_GITHUB_REPO"] ?? "kavinrahal/job-search-agent";
// Deliberately low. Each dispatch is a full Claude Code session, and the failure mode worth
// guarding is a bad deploy producing many distinct new issues at once — see CrashTriage.
const int CrashFixHourlyCap = 3;

builder.Services.AddSingleton(_ => new JobPostingFetcher());
builder.Services.AddSingleton(sp => new ClaudeUsageLogger(sp.GetRequiredService<DbContextOptions<AppDbContext>>()));
builder.Services.AddSingleton(sp => new PostingEvaluator(anthropicApiKey, sp.GetRequiredService<ClaudeUsageLogger>()));
builder.Services.AddSingleton(sp => new CoverLetterAgent(anthropicApiKey, sp.GetRequiredService<ClaudeUsageLogger>()));
builder.Services.AddSingleton(sp => new CvTailorAgent(anthropicApiKey, sp.GetRequiredService<ClaudeUsageLogger>()));
builder.Services.AddSingleton(sp => new AnswerAgent(anthropicApiKey, sp.GetRequiredService<ClaudeUsageLogger>()));
builder.Services.AddSingleton(sp => new ResumeIntakeAgent(anthropicApiKey, sp.GetRequiredService<ClaudeUsageLogger>()));
builder.Services.AddSingleton(sp => new ResumeBackfillAgent(anthropicApiKey, sp.GetRequiredService<ClaudeUsageLogger>()));
builder.Services.AddSingleton(sp => new ResumeSummaryAgent(anthropicApiKey, sp.GetRequiredService<ClaudeUsageLogger>()));
builder.Services.AddSingleton(sp => new PostingMatcherAgent(anthropicApiKey, sp.GetRequiredService<ClaudeUsageLogger>()));
builder.Services.AddSingleton(sp => new CompanyExtractorAgent(anthropicApiKey, sp.GetRequiredService<ClaudeUsageLogger>()));
builder.Services.AddSingleton(sp => new AccuracyVerifierAgent(anthropicApiKey, sp.GetRequiredService<ClaudeUsageLogger>()));
builder.Services.AddSingleton(_ => new JoraFetcher());
// Left unregistered when unset. Deliberately NOT an endpoint parameter (AdzunaFetcher?) —
// minimal API infers an unregistered type's parameter source at startup from the DI
// container's contents, so an endpoint requesting a type that isn't registered fails with a
// 500 ("did you mean to register this as a service?") instead of resolving to null. Resolved
// per-request instead via ctx.RequestServices.GetService<AdzunaFetcher>(), which does return
// null safely for an unregistered type — this is plain IServiceProvider behavior, not a
// minimal-API inference decision made once at startup.
if (adzunaAppId is not null && adzunaAppKey is not null)
    builder.Services.AddSingleton(new AdzunaFetcher(adzunaAppId, adzunaAppKey));
if (gmailClientId is not null && gmailClientSecret is not null && gmailOAuthRedirectUri is not null)
    builder.Services.AddSingleton(new GmailOAuthService(gmailClientId, gmailClientSecret, gmailOAuthRedirectUri));
// Same Gmail OAuth client as above, no redirect URI needed here — this only ever calls the
// Settings API (filters, forwarding addresses) with an already-stored refresh token, not a
// fresh consent round-trip.
if (gmailClientId is not null && gmailClientSecret is not null)
    builder.Services.AddSingleton(new GmailSettingsClient(gmailClientId, gmailClientSecret));
if (sendGridApiKey is not null && sendGridFromEmail is not null)
    builder.Services.AddSingleton(new SendGridEmailService(sendGridApiKey, sendGridFromEmail));

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

    // Public webhooks (SendGrid inbound, Sentry): unauthenticated (secret-verified), internet-facing.
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

    // Password register/login/forgot-password: no authenticated user yet to partition by
    // (unlike "generation" above), so partitioned per IP instead — the thing worth limiting
    // is one attacker guessing passwords or spamming registrations, not one legitimate user
    // mistyping a password a couple of times. Same fixed-window shape as "webhook", just
    // partitioned. Not applied to verify-email/reset-password — those are already gated by a
    // hard-to-guess, single-use token, so the token itself is the rate limit that matters.
    o.AddPolicy("auth", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
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

// Must come after UseCors so its response (a clean JSON 500) still flows back out through
// CORS's response-header logic. Without this, an unhandled exception fell through to the
// framework's bare default handler, whose response carries no CORS headers — the browser's
// Network tab shows the real status code, but fetch() itself gets blocked from reading it and
// throws a generic "Failed to fetch" instead, hiding the actual error from both the user and
// the frontend's own error handling. Real production incident: a resume-parsing 500 (a genuine
// bug) was reported as "Failed to fetch" with no other information, only diagnosable by reading
// server logs directly.
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    await Console.Error.WriteLineAsync($"[UnhandledException] {ex}");
    ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await ctx.Response.WriteAsJsonAsync(new { error = "Something went wrong. Please try again." });
}));

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
// because the owner-only admin endpoints below (see ownerUserId usages) need a stable id
// to compare against.
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
    // After the OAuth round-trip, redirect back to the SPA — its own separate deployment in
    // prod (frontendUrl), Vite's own port in dev.
    var redirectUri = isDev ? "http://localhost:5173/" : frontendUrl!;
    return Results.Challenge(
        new AuthenticationProperties { RedirectUri = redirectUri },
        [GoogleDefaults.AuthenticationScheme]);
}).AllowAnonymous();

app.MapGet("/api/v1/auth/me", async (HttpContext ctx, AppDbContext db) =>
{
    var uidClaim = ctx.User.FindFirstValue(UserIdClaimType);
    // TEMP diagnostic: RequireAuthorization() is passing (we got here) but the app-specific
    // uid claim is missing — actively investigating a live bug where fresh logins bounce back
    // to the landing page. Logging every claim actually present tells us whether OnCreatingTicket
    // ran at all versus ran but didn't persist this one claim. Return 401 cleanly either way
    // instead of crashing with an unhandled ArgumentNullException.
    if (uidClaim is null)
    {
        var claims = string.Join("; ", ctx.User.Claims.Select(c => $"{c.Type}={c.Value}"));
        await Console.Error.WriteLineAsync($"[AuthDiag] /auth/me: authenticated={ctx.User.Identity?.IsAuthenticated}, authType={ctx.User.Identity?.AuthenticationType}, claims=[{claims}]");
        return Results.Unauthorized();
    }
    var userId = int.Parse(uidClaim, CultureInfo.InvariantCulture);
    var user = await db.Users.FindAsync(userId);
    if (user is null) return Results.Unauthorized();

    // A blank Background is exactly the state the login handler creates for a brand new
    // user (see UserProfileProvisioningService.GetOrSeedAsync call in OnCreatingTicket) — the
    // owner's is always seeded with real content, so this only ever flags a genuine first-timer.
    var profile = await db.UserProfiles.FindAsync(userId);
    bool needsOnboarding = string.IsNullOrEmpty(profile?.Background);
    // Same "engaged with the step once" semantics as needsSourceSelection below, not "filled
    // in something meaningful" — saving from JobCriteriaPage always writes a full YAML
    // skeleton (defaults included), so this only stays true until the user visits and saves
    // at least once. The dashboard's separate nudge banner checks for actually-empty content.
    //
    // Tier 2 additionally requires target_job_titles specifically — it's hidden from Tier 1
    // (see JobCriteriaEditor.tsx), since it only drives Tier2's automatic discovery. This is
    // what makes upgrading to Tier 2 correctly route someone back through the criteria step
    // (JobCriteriaPage now shows the field) if they upgraded without ever filling it in —
    // the same redirect machinery that already exists for the initial onboarding flow.
    bool needsCriteria = string.IsNullOrEmpty(profile?.JobCriteria)
        || (user.Tier == UserTier.Tier2 && TargetJobTitles.Parse(profile?.JobCriteria).Length == 0);
    bool needsSourceSelection = user.Tier == UserTier.Tier2 && user.EnabledSources is null;
    bool isOwner = string.Equals(user.Email, ownerEmail, StringComparison.OrdinalIgnoreCase);
    // First name only, for a casual dashboard greeting — null until Background exists (a brand
    // new user mid-onboarding), same source as BuildDownloadFilename's applicant name below.
    string? firstName = ExtractApplicantName(profile?.Background)?.Split(' ', 2)[0];

    return Results.Ok(new { user.Id, user.Email, user.Tier, user.CreditBalance, needsOnboarding, needsCriteria, needsSourceSelection, isOwner, firstName });
}).RequireAuthorization();

app.MapPost("/api/v1/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok();
}).RequireAuthorization();

// ---------------------------------------------------------------------------
// Password auth — second, fully independent login path alongside Google OAuth above.
// Nothing here changes Google's own endpoints/handlers; the only touchpoint is
// OnCreatingTicket now also stamping User.EmailVerifiedAt (see comment there), which is what
// lets a password registration against an email that already logged in via Google skip
// re-verification (the account-linking safety net — see PasswordAuthService).
// ---------------------------------------------------------------------------

// Builds the exact same UserIdClaimType claim Google's OnCreatingTicket stamps onto its own
// identity (see const above), so every downstream CurrentUserId(ctx, UserIdClaimType) call —
// which is how every protected endpoint, including Sources/Gmail-connect, resolves the
// caller — behaves identically regardless of which path signed the user in.
//
// Returns false (and never signs in) for a cancelled account — the password path's own
// equivalent of Google OnCreatingTicket's `if (user.DeactivatedAt is not null) ctx.Fail(...)`
// above. Without this, POST /account/cancel's "signs you out... you just won't be able to
// sign in again" promise only held for Google sign-in: a cancelled user could undo their own
// cancellation just by logging back in with their existing password (LoginAsync never checked
// DeactivatedAt), or — for an account that had proved ownership via a prior Google login but
// never set a password — by registering a fresh password against the same email (RegisterAsync's
// SignedIn branch). Every one of this method's four callers goes through here, so this one
// check closes all of them at once.
static async Task<bool> TrySignInPasswordUserAsync(HttpContext ctx, User user)
{
    if (user.DeactivatedAt is not null) return false;

    var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
    identity.AddClaim(new Claim(UserIdClaimType, user.Id.ToString(CultureInfo.InvariantCulture)));
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    return true;
}

// Shared response for every password-path entry point once a cancelled account is caught —
// deliberately not the generic "invalid email or password" InvalidCredentials message, since
// the caller here already proved they own the account (correct password, or a just-consumed
// verification/reset token); telling them it's cancelled is more honest than pretending they
// mistyped something, and matches what the cancel flow's own UI copy already tells them to
// expect ("you just won't be able to sign in again unless it's reactivated"). Doesn't point
// them at the in-app Support form — that's an authenticated-only endpoint (see POST /support
// below), which is exactly the thing a cancelled, signed-out visitor can't reach.
const string AccountCancelledMessage = "This account has been cancelled and can't be signed into.";

// POST /api/v1/auth/register — { email, password }. Gated by the same beta-invite rules
// Google login uses (BetaAccessService) and resolves through the same GetOrCreateAsync
// account lookup, so registering with an email that already has a Google-created row attaches
// to that row instead of creating a duplicate. A brand-new (or previously Google-unverified)
// account gets emailed a verification link and is NOT signed in yet; an email that already
// proved ownership via a prior Google login is signed in immediately.
app.MapPost("/api/v1/auth/register", async (HttpContext ctx, RegisterRequest body, AppDbContext db) =>
{
    // Request DTOs are positional records with non-nullable string params, but that's a
    // compile-time-only guarantee — a missing/null JSON field still binds to null at runtime,
    // so this guard is load-bearing, not redundant. Without it, PasswordRules.Validate(null)
    // throws a NullReferenceException instead of a clean 400.
    if (string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Password))
        return Results.BadRequest(new { error = "Email and password are required." });

    var result = await PasswordAuthService.RegisterAsync(db, body.Email, body.Password, ownerEmail);

    switch (result.Outcome)
    {
        case PasswordAuthService.RegisterOutcome.PasswordInvalid:
            return Results.BadRequest(new { errors = result.PasswordErrors });

        case PasswordAuthService.RegisterOutcome.NotInvited:
            return Results.BadRequest(new { error = "This email hasn't been invited yet." });

        case PasswordAuthService.RegisterOutcome.AlreadyHasPassword:
            return Results.BadRequest(new { error = "This email already has a password. Log in, or use forgot password." });

        case PasswordAuthService.RegisterOutcome.SignedIn:
            if (!await TrySignInPasswordUserAsync(ctx, result.User!))
                return Results.BadRequest(new { error = AccountCancelledMessage });
            return Results.Ok(new { status = "signed_in" });

        default: // RegisterOutcome.VerificationSent — the normal case for a genuinely new user
            var apiBase = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
            var verifyLink = $"{apiBase}/api/v1/auth/verify-email?token={result.VerificationToken}";
            var sender = ctx.RequestServices.GetService<SendGridEmailService>();
            if (sender is not null)
            {
                try
                {
                    await sender.SendAsync(result.User!.Email, "Verify your email for Work Santa",
                        $"Click this link to verify your email and activate your account: {verifyLink}\n\n" +
                        "This link expires in 1 hour.");
                }
                catch (Exception ex)
                {
                    await Console.Error.WriteLineAsync($"Failed to send verification email to {result.User!.Email}: {ex}");
                }
            }
            return Results.Ok(new { status = "verification_sent" });
    }
}).RequireRateLimiting("auth").AllowAnonymous();

// GET /api/v1/auth/verify-email?token=... — consumes the token, marks the email verified,
// signs the user in, and redirects to the frontend root, same redirect-based pattern the
// Google OAuth callback/failure handlers above already use — landing on "/" with a valid
// session cookie already works today via useMe(), so no new frontend routing is needed.
app.MapGet("/api/v1/auth/verify-email", async (HttpContext ctx, AppDbContext db, string token) =>
{
    var redirectBase = (frontendUrl ?? "http://localhost:5173").TrimEnd('/');

    var user = await PasswordAuthService.VerifyEmailAsync(db, token);
    if (user is null)
        return Results.Redirect($"{redirectBase}/?authError=invalid_token");

    if (!await TrySignInPasswordUserAsync(ctx, user))
        return Results.Redirect($"{redirectBase}/?authError=deactivated");
    return Results.Redirect(redirectBase);
}).RequireRateLimiting("auth").AllowAnonymous();

// POST /api/v1/auth/login — { email, password }. Same generic "invalid email or password" for
// every failure reason (unknown email, Google-only account with no password, wrong password)
// — deliberately not distinguishing which, to avoid leaking which emails have accounts.
app.MapPost("/api/v1/auth/login", async (HttpContext ctx, LoginRequest body, AppDbContext db) =>
{
    // Same null-guard reasoning as /auth/register above — a missing field binds to null at
    // runtime despite the non-nullable DTO param, and FindByNormalizedEmailAsync's
    // email.Trim() would otherwise throw instead of returning a clean 400.
    if (string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Password))
        return Results.BadRequest(new { error = "Email and password are required." });

    var result = await PasswordAuthService.LoginAsync(db, body.Email, body.Password);

    switch (result.Outcome)
    {
        case PasswordAuthService.LoginOutcome.InvalidCredentials:
            return Results.BadRequest(new { error = "Invalid email or password." });

        case PasswordAuthService.LoginOutcome.NotVerified:
            return Results.BadRequest(new { error = "Verify your email first — check your inbox for the link." });

        default:
            if (!await TrySignInPasswordUserAsync(ctx, result.User!))
                return Results.BadRequest(new { error = AccountCancelledMessage });
            return Results.Ok();
    }
}).RequireRateLimiting("auth").AllowAnonymous();

// POST /api/v1/auth/forgot-password — { email }. Always the same generic response regardless
// of whether the email exists or has a password set — prevents enumeration. Only sends an
// email when there's actually a password-holding account to reset.
app.MapPost("/api/v1/auth/forgot-password", async (HttpContext ctx, ForgotPasswordRequest body, AppDbContext db) =>
{
    var (user, token) = await PasswordAuthService.RequestPasswordResetAsync(db, body.Email);

    if (user is not null && token is not null)
    {
        var redirectBase = (frontendUrl ?? "http://localhost:5173").TrimEnd('/');
        var resetLink = $"{redirectBase}/?resetToken={token}";
        var sender = ctx.RequestServices.GetService<SendGridEmailService>();
        if (sender is not null)
        {
            try
            {
                await sender.SendAsync(user.Email, "Reset your Work Santa password",
                    $"Click this link to set a new password: {resetLink}\n\nThis link expires in 1 hour. " +
                    "If you didn't request this, you can ignore this email.");
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Failed to send password reset email to {user.Email}: {ex}");
            }
        }
    }

    return Results.Ok(new { message = "If that email has an account, we've sent a reset link." });
}).RequireRateLimiting("auth").AllowAnonymous();

// POST /api/v1/auth/reset-password — { token, newPassword }.
app.MapPost("/api/v1/auth/reset-password", async (HttpContext ctx, ResetPasswordRequest body, AppDbContext db) =>
{
    var result = await PasswordAuthService.ResetPasswordAsync(db, body.Token, body.NewPassword);

    switch (result.Outcome)
    {
        case PasswordAuthService.ResetPasswordOutcome.PasswordInvalid:
            return Results.BadRequest(new { errors = result.PasswordErrors });

        case PasswordAuthService.ResetPasswordOutcome.InvalidToken:
            return Results.BadRequest(new { error = "This reset link is invalid or has expired." });

        default:
            if (!await TrySignInPasswordUserAsync(ctx, result.User!))
                return Results.BadRequest(new { error = AccountCancelledMessage });
            return Results.Ok();
    }
}).RequireRateLimiting("auth").AllowAnonymous();

// ---------------------------------------------------------------------------
// Protected data endpoints
// ---------------------------------------------------------------------------
var api = app.MapGroup("/api/v1").RequireAuthorization();

// GET /api/v1/discoveries — Tier 2 only: discovery is a Tier 2-exclusive feature.
api.MapGet("/discoveries", async (HttpContext ctx, AppDbContext db, string? recommendation = null, int page = 1, int pageSize = 25) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    var (_, tierError) = await RequireTier2Async(db, userId);
    if (tierError is not null) return tierError;

    var validRecs = new HashSet<string> { "strong_match", "good_match", "weak_match", "discard" };
    if (recommendation is not null && !validRecs.Contains(recommendation))
        return Results.BadRequest(new { error = "Invalid recommendation value" });

    // page <= 0 would otherwise produce a negative Skip()/OFFSET and 500 in Postgres; pageSize
    // has no natural upper bound from the caller otherwise. Same clamp as /applications below.
    page = Math.Max(1, page);
    pageSize = Math.Clamp(pageSize, 1, 100);

    // Explicit ownership scoping — defense-in-depth alongside AppDbContext's global query
    // filter (HasQueryFilter, OnModelCreating), which already restricts this to the
    // caller's own rows; every other explicit UserId check/filter added below is the same
    // belt-and-suspenders reasoning, not repeated per site.
    var query = db.DiscoveredPostings.AsQueryable().Where(d => d.UserId == userId);

    // No explicit recommendation = the "All" tab, which should still never surface discards —
    // the user should never see something the agent decided didn't meet their criteria. Doing
    // this exclusion here (before Skip/Take) matters: filtering it out client-side after
    // pagination instead would silently drop real strong/good/weak matches whenever discards
    // dominate the most recent page, since they'd get paginated away before the client-side
    // filter ever saw them.
    query = recommendation is not null
        ? query.Where(d => d.Recommendation == recommendation)
        : query.Where(d => d.Recommendation != null && d.Recommendation != "error" && d.Recommendation != "discard");

    int total = await query.CountAsync();

    var raw = await query
        .OrderByDescending(d => d.DiscoveredAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

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

// GET /api/v1/summary — Tier 2 only: application-tracking KPIs. Deliberately excludes any
// email-content-derived counts (total/classified/job-related/by-category) — those were a
// window into inbox content, at odds with users who pick filter-only or manual Gmail
// tracking (see GmailTrackingMode). Applications data is the user's own tracked-application
// records, not inbox content, so it's fine to summarize.
api.MapGet("/summary", async (HttpContext ctx, AppDbContext db) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    var (_, tierError) = await RequireTier2Async(db, userId);
    if (tierError is not null) return tierError;

    // Explicit ownership scoping, defense-in-depth (see the /discoveries comment above).
    var ownApplications = db.Applications.Where(a => a.UserId == userId);

    var appsByStatus = await ownApplications
        .GroupBy(a => a.Status)
        .Select(g => new { Status = g.Key, Count = g.Count() })
        .ToListAsync();

    int total = await ownApplications.CountAsync();

    // 7-day cumulative history for the Today dashboard's stat-block sparklines (real trend, not
    // the old fixed mock shape). Per-user data volumes here are small (a personal job-tracking
    // app, not a scale product), so this loads applications with their events eagerly and does
    // the day-by-day computation in plain C# rather than fighting SQL for it.
    var appsWithEvents = await ownApplications
        .Include(a => a.Events)
        .ToListAsync();

    var today = DateTime.UtcNow.Date;
    var history = new List<object>();
    for (int daysAgo = 6; daysAgo >= 0; daysAgo--)
    {
        var day = today.AddDays(-daysAgo);
        var existingByDay = appsWithEvents.Where(a => a.AppliedAt.Date <= day).ToList();
        int applicationsSentThatDay = existingByDay.Count;

        int repliedThatDay = 0;
        foreach (var app in existingByDay)
        {
            // Status as of `day`: the most recent event with OccurredAt.Date <= day and a
            // non-null ToStatus. Falls back to Applied if none exists yet — every application's
            // creation path (ApplicationTracker.FindOrCreateAsync and POST /applications) always
            // logs a creation event with ToStatus=Applied at the same moment as AppliedAt, so
            // this fallback is only theoretical, never actually exercised in practice.
            string statusAsOfDay = app.Events
                .Where(e => e.OccurredAt.Date <= day && e.ToStatus != null)
                .OrderByDescending(e => e.OccurredAt)
                .ThenByDescending(e => e.Id)
                .Select(e => e.ToStatus!)
                .FirstOrDefault() ?? ApplicationStatus.Applied;

            // Same "replied" definition the KPIs above use: anything past the initial
            // "sent, no response yet" stages counts as a reply.
            if (statusAsOfDay != ApplicationStatus.Applied && statusAsOfDay != ApplicationStatus.Acknowledged)
                repliedThatDay++;
        }

        int replyRateThatDay = applicationsSentThatDay > 0
            ? (int)Math.Round(repliedThatDay / (double)applicationsSentThatDay * 100, MidpointRounding.AwayFromZero)
            : 0;

        history.Add(new
        {
            date = day.ToString("yyyy-MM-dd"),
            applicationsSent = applicationsSentThatDay,
            replyRate = replyRateThatDay,
        });
    }

    return Results.Ok(new
    {
        applications = new
        {
            total,
            byStatus = appsByStatus.ToDictionary(x => x.Status, x => x.Count),
        },
        history,
    });
});

// GET /api/v1/applications — Tier 2 only: application tracking is a Tier 2-exclusive feature.
api.MapGet("/applications", async (
    HttpContext ctx,
    AppDbContext db,
    string? status = null,
    int page = 1,
    int pageSize = 25) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    var (_, tierError) = await RequireTier2Async(db, userId);
    if (tierError is not null) return tierError;

    if (status is not null && !ApplicationStatus.All.Contains(status))
        return Results.BadRequest(new { error = "Invalid status" });

    // page <= 0 would otherwise produce a negative Skip()/OFFSET and 500 in Postgres; pageSize
    // has no natural upper bound from the caller otherwise. Same clamp as /discoveries above.
    page = Math.Max(1, page);
    pageSize = Math.Clamp(pageSize, 1, 100);

    // Explicit ownership scoping, defense-in-depth (see the /discoveries comment above).
    var query = db.Applications.AsQueryable().Where(a => a.UserId == userId);
    if (status is not null)
        query = query.Where(a => a.Status == status);

    int total = await query.CountAsync();

    var items = await query
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
        .ToListAsync();

    return Results.Ok(new { items, total, page, pageSize });
});

// GET /api/v1/applications/{id}/events — Tier 2 only.
api.MapGet("/applications/{id}/events", async (HttpContext ctx, int id, AppDbContext db) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    var (_, tierError) = await RequireTier2Async(db, userId);
    if (tierError is not null) return tierError;

    // Explicit ownership check, defense-in-depth: AppDbContext's global query filter
    // (HasQueryFilter, OnModelCreating) already makes FindAsync return null for a row
    // owned by another user; this re-checks it explicitly at the call site too. NotFound,
    // not Forbidden, so a caller can't distinguish "doesn't exist" from "someone else's" —
    // same reasoning at every other FindAsync-by-id site touched below.
    var application = await db.Applications.FindAsync(id);
    if (application is null || application.UserId != userId) return Results.NotFound();

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

// POST /api/v1/applications — Tier 2 only. The manual counterpart to ApplicationTracker's
// email-driven FindOrCreateAsync — lets a user log an application directly instead of
// waiting for a matching email. CompanyDomain (filter tracking mode only) is stored on the
// row so a later classified email can match it even if the company name comes through
// slightly differently worded (see ApplicationTracker.FindOrCreateAsync).
api.MapPost("/applications", async (HttpContext ctx, AppDbContext db, UserSecretService secrets, CreateApplicationRequest body) =>
{
    var (user, tierError) = await RequireTier2Async(db, CurrentUserId(ctx, UserIdClaimType));
    if (tierError is not null) return tierError;

    if (string.IsNullOrWhiteSpace(body.Company) || string.IsNullOrWhiteSpace(body.RoleTitle))
        return Results.BadRequest(new { error = "Company and role title are required." });

    var status = body.Status is not null && ApplicationStatus.All.Contains(body.Status)
        ? body.Status
        : ApplicationStatus.Applied;

    var now = DateTime.UtcNow;
    var application = new Application
    {
        UserId = user!.Id,
        Company = body.Company.Trim(),
        RoleTitle = body.RoleTitle.Trim(),
        JobUrl = body.JobUrl,
        CompanyDomain = body.CompanyDomain?.Trim().ToLowerInvariant(),
        Status = status,
        AppliedAt = now,
        UpdatedAt = now,
    };
    db.Applications.Add(application);
    await db.SaveChangesAsync(); // flush to get application.Id before adding the event

    db.ApplicationEvents.Add(new ApplicationEvent
    {
        UserId = user.Id,
        ApplicationId = application.Id,
        EventType = ApplicationEventType.ManualUpdate,
        FromStatus = null,
        ToStatus = status,
        Summary = $"Manually logged: {application.Company} - {application.RoleTitle}",
        OccurredAt = now,
    });
    await db.SaveChangesAsync();

    // Filter tracking mode: install a Gmail filter forwarding this company's domain, the
    // same way job-alert forwarding already works. Best-effort — a failure here shouldn't
    // fail the application creation itself, since the app row is already saved either way.
    if (application.CompanyDomain is not null
        && user.GmailTrackingMode == GmailTrackingMode.Filter
        && sendGridInboundDomain is not null)
    {
        var gmailSettings = ctx.RequestServices.GetService<GmailSettingsClient>();
        var refreshToken = gmailSettings is null ? null : await secrets.GetAsync(db, user.Id, UserSecretKey.GmailSettingsRefreshToken);
        if (gmailSettings is not null && refreshToken is not null)
        {
            try
            {
                var address = await InboundEmailService.GetOrCreateAddressAsync(db, user.Id, sendGridInboundDomain);
                await gmailSettings.EnsureCompanyFilterAsync(refreshToken, application.CompanyDomain, address);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"EnsureCompanyFilterAsync failed for user {user.Id}: {ex}");
            }
        }
    }

    return Results.Ok(new
    {
        id = application.Id,
        company = application.Company,
        roleTitle = application.RoleTitle,
        jobUrl = application.JobUrl,
        status = application.Status,
        appliedAt = application.AppliedAt,
        updatedAt = application.UpdatedAt,
        notes = application.Notes,
    });
});

// PATCH /api/v1/applications/{id} — Tier 2 only. Manual status correction — deliberately not
// forward-only like ApplicationTracker.CanAdvanceTo: a user correcting their own application's
// status is trusted, unlike an inferred email classification, so any valid status is allowed
// including moving backward.
api.MapPatch("/applications/{id}", async (HttpContext ctx, int id, AppDbContext db, UpdateApplicationStatusRequest body) =>
{
    var (user, tierError) = await RequireTier2Async(db, CurrentUserId(ctx, UserIdClaimType));
    if (tierError is not null) return tierError;

    if (!ApplicationStatus.All.Contains(body.Status))
        return Results.BadRequest(new { error = "Invalid status" });

    // Explicit ownership check, defense-in-depth (see /applications/{id}/events above).
    var application = await db.Applications.FindAsync(id);
    if (application is null || application.UserId != user!.Id) return Results.NotFound();

    if (application.Status != body.Status)
    {
        var now = DateTime.UtcNow;
        db.ApplicationEvents.Add(new ApplicationEvent
        {
            UserId = user!.Id,
            ApplicationId = application.Id,
            EventType = ApplicationEventType.ManualUpdate,
            FromStatus = application.Status,
            ToStatus = body.Status,
            Summary = $"Manually updated: {application.Status} → {body.Status}",
            OccurredAt = now,
        });
        application.Status = body.Status;
        application.UpdatedAt = now;
        await db.SaveChangesAsync();
    }

    return Results.Ok(new { id = application.Id, status = application.Status, updatedAt = application.UpdatedAt });
});

// GET /api/v1/activity — Tier 2 only: built entirely from application-tracking events.
api.MapGet("/activity", async (HttpContext ctx, AppDbContext db, int limit = 20) =>
{
    var (_, tierError) = await RequireTier2Async(db, CurrentUserId(ctx, UserIdClaimType));
    if (tierError is not null) return tierError;

    var items = await db.ApplicationEvents
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
        .ToListAsync();

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
    };

    return status == "stale"
        ? Results.Json(result, statusCode: 503)
        : Results.Ok(result);
}).AllowAnonymous(); // UptimeRobot hits this without a session

// GET /api/v1/status — maintenance-mode/banner state, read by JobSearch.Web's useSiteStatus()
// before anything else mounts. Must stay unauthenticated and cheap: a logged-out visitor has
// to see maintenance mode too, and this is written by AdminDashboard.Api even when this API
// itself is degraded — see SiteStatus's own doc comment for why it's always exactly one row.
api.MapGet("/status", async (AppDbContext db) =>
{
    var status = await db.SiteStatuses.SingleAsync();
    return Results.Ok(new
    {
        maintenanceMode = status.MaintenanceMode,
        maintenanceMessage = status.MaintenanceMessage,
        bannerActive = status.BannerActive,
        bannerMessage = status.BannerMessage,
    });
}).AllowAnonymous();

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

    // A missing/null message binds at runtime despite the DTO's non-nullable string param —
    // without this guard it reaches SaveChangesAsync as a null on a required column instead of
    // a clean 400.
    if (string.IsNullOrWhiteSpace(body.Message))
        return Results.BadRequest(new { error = "message is required." });

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

// GET /api/v1/admin/diagnose-fetch?url=... — owner-only. Reports exactly what happened
// fetching a URL from this deployed environment (status code, size, whether the response
// looks like a bot-challenge page) for both the direct request and the reader-proxy
// fallback, instead of just the final text a normal fetch returns — for confirming from
// production itself why a source like Seek is or isn't fetchable, rather than inferring it
// from a developer machine's very different IP reputation.
api.MapGet("/admin/diagnose-fetch", async (HttpContext ctx, JobPostingFetcher fetcher, string url) =>
{
    if (CurrentUserId(ctx, UserIdClaimType) != ownerUserId)
        return Results.Json(new { error = "Forbidden" }, statusCode: StatusCodes.Status403Forbidden);

    var d = await fetcher.DiagnoseAsync(url);
    var preview = d.ResultText is { } text ? text[..Math.Min(500, text.Length)] : null;
    return Results.Ok(new { d.Direct, d.Reader, ResultPreview = preview });
});

// ---------------------------------------------------------------------------
// Onboarding — resume parsing, and saving the result to a profile.
// ---------------------------------------------------------------------------

// POST /api/v1/onboarding/parse-resume — multipart form: either a "text" field (pasted
// resume) or a "file" field (PDF). Returns a preview for the user to review/edit — nothing
// is persisted here, including the PDF itself: persisting it immediately (before the user
// has reviewed/saved anything) would leave the stored PDF and the stored CV text
// inconsistent with each other until — or unless — they actually hit Save. The PDF is sent
// again at save time instead (see POST /profile/resume-pdf), so both land together.
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
        try
        {
            parsed = await intakeAgent.ParseFromPdfAsync(userId, ms.ToArray());
        }
        catch (PdfTextExtractionException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
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
    return Results.Ok(new
    {
        profile.Background, profile.CvBase, profile.JobCriteria, profile.UpdatedAt,
        hasResumePdf = profile.ResumePdf is not null,
    });
});

// GET /api/v1/profile/resume-pdf — the original uploaded PDF, for the dashboard's viewer.
// 404 if the user has only ever pasted text (no file was ever uploaded).
api.MapGet("/profile/resume-pdf", async (HttpContext ctx, AppDbContext db) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    var profile = await db.UserProfiles.FindAsync(userId);
    if (profile?.ResumePdf is not { } pdf) return Results.NotFound();
    return Results.File(pdf, "application/pdf");
});

// POST /api/v1/profile/resume-pdf — multipart form, "file" field. Called alongside PUT
// /profile at save time (not at parse time — see the comment on /onboarding/parse-resume).
api.MapPost("/profile/resume-pdf", async (HttpRequest request, HttpContext ctx, AppDbContext db) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    var form = await request.ReadFormAsync();
    var file = form.Files["file"];
    if (file is not { Length: > 0 }) return Results.BadRequest(new { error = "Provide a \"file\" (PDF) field." });

    var profile = await db.UserProfiles.FindAsync(userId);
    if (profile is null) return Results.NotFound();

    using var ms = new MemoryStream();
    await file.CopyToAsync(ms);
    profile.ResumePdf = ms.ToArray();
    await db.SaveChangesAsync();

    return Results.Ok();
}).RequireRateLimiting("generation");

// PUT /api/v1/profile — partial update: only provided fields change.
api.MapPut("/profile", async (HttpContext ctx, ProfileUpdateRequest body, AppDbContext db, ResumeBackfillAgent backfillAgent) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    var profile = await db.UserProfiles.FindAsync(userId);
    if (profile is null) return Results.NotFound();

    if (body.Background is not null) profile.Background = body.Background;
    if (body.CvBase is not null) profile.CvBase = body.CvBase;
    if (body.JobCriteria is not null) profile.JobCriteria = body.JobCriteria;
    profile.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    // A fresh CvBase means this save is a resume intake/replace event (it's the only way CvBase
    // ever gets set — see ResumeIntakePage.tsx/SettingsPage.tsx, there's no direct text editor
    // for it). Reuses the exact same reconciliation the admin backfill endpoint uses, so a
    // brand-new signup during the Deploy A/B rollout window still ends up with a UserResume row
    // by the time Deploy B makes one required — same call, not a separate mechanism. Best-effort:
    // a transient failure here must not fail the profile save itself (Background/CvBase already
    // committed above); the owner can retry via POST /admin/users/{id}/backfill-resume, same as
    // any other not-yet-migrated account.
    if (body.CvBase is not null)
    {
        try
        {
            db.UserResumes.RemoveRange(db.UserResumes.Where(r => r.UserId == userId));
            var result = await backfillAgent.BackfillAsync(userId, profile.Background, profile.CvBase);
            db.UserResumes.Add(BuildUserResume(userId, result));
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"UserResume seeding failed for user {userId}, will need a manual admin backfill: {ex}");
        }
    }
    // From-scratch onboarding save (a Background was actually being set, but there's no CvBase
    // to reconcile — see ResumeIntakePage.tsx's "build from scratch" entry point and
    // UserResumeProvisioningService's own comment for why this is deterministic, not an agent
    // call). Only seeds once — a user who later replaces CvBase for real still goes through the
    // branch above, which unconditionally overwrites via RemoveRange + Add.
    else if (body.Background is not null)
    {
        await UserResumeProvisioningService.SeedDefaultIfMissingAsync(db, userId);
    }

    return Results.Ok(new { profile.Background, profile.CvBase, profile.JobCriteria, profile.UpdatedAt });
});

// GET /api/v1/resume-templates — static per-industry SectionConfig defaults for the resume
// builder's industry picker (resume-builder Phase 2). No DB hit — this is a fixed catalog, not
// per-user data.
api.MapGet("/resume-templates", () => Results.Ok(new
{
    industries = ResumeIndustryTemplates.All.Select(t => new { t.Key, t.DisplayName, t.HasSeniorityToggle }),
}));

// GET /api/v1/resume — the current user's full UserResume curation data (Summary, SectionConfig,
// and the per-experience/project/skills overrides), for the resume builder. 409 (same message
// /cv already uses) if the row doesn't exist yet —
// UserResume isn't guaranteed to exist the moment a UserProfile does, see PUT /profile's
// best-effort seeding comment above.
api.MapGet("/resume", async (HttpContext ctx, AppDbContext db) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    var resume = await db.UserResumes.FindAsync(userId);
    return resume is null ? ResumeNotReadyError() : ResumeResponse(resume);
});

// PUT /api/v1/resume — partial update: only provided fields change. Manual section
// include/reorder edits, Summary edits, and per-experience/project/skills curation edits all
// go through this one endpoint.
api.MapPut("/resume", async (HttpContext ctx, ResumeUpdateRequest body, AppDbContext db) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    var resume = await db.UserResumes.FindAsync(userId);
    if (resume is null) return ResumeNotReadyError();

    ResumeCuration.ApplyUpdate(resume, body.Summary, body.SectionConfig, body.ExperienceOverrides, body.ProjectOverrides, body.SkillsSection);
    resume.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return ResumeResponse(resume);
});

// POST /api/v1/resume/preview — the resume builder's live preview. Takes the same draft shape
// as PUT /resume but never saves it: builds a transient UserResume (draft fields where
// provided, the user's real stored UserResume otherwise) and renders it with the real
// ResumeRenderer, so the preview is guaranteed to match what a Save would actually produce
// instead of a second, drifting reimplementation of the merge logic. See ResumeCuration.Preview.
api.MapPost("/resume/preview", async (HttpContext ctx, ResumeUpdateRequest body, AppDbContext db) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    var resume = await db.UserResumes.FindAsync(userId);
    if (resume is null) return ResumeNotReadyError();

    var profile = await db.UserProfiles.FindAsync(userId)
        ?? throw new InvalidOperationException("UserProfile not found for the current user.");
    var background = BackgroundYamlParser.Parse(profile.Background);

    var markdown = ResumeCuration.Preview(background, resume, body.Summary, body.SectionConfig, body.ExperienceOverrides, body.ProjectOverrides, body.SkillsSection);
    return Results.Ok(new { markdown });
});

// POST /api/v1/resume/apply-template — seeds SectionConfigJson from a chosen industry's
// default order (+ seniority, for the industries research found a junior/experienced split
// for). Overwrites only SectionConfigJson — Summary/ExperienceOverrides/SkillsSection/
// ProjectOverrides are untouched, so applying a template never discards curation work already
// done elsewhere.
api.MapPost("/resume/apply-template", async (HttpContext ctx, ApplyResumeTemplateRequest body, AppDbContext db) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    var template = ResumeIndustryTemplates.Find(body.IndustryKey);
    if (template is null)
        return Results.BadRequest(new { error = $"Unknown industry \"{body.IndustryKey}\"." });

    var seniority = body.Seniority?.ToLowerInvariant() switch
    {
        "junior" => ResumeSeniority.Junior,
        "experienced" or null => ResumeSeniority.Experienced,
        _ => (ResumeSeniority?)null,
    };
    if (seniority is null)
        return Results.BadRequest(new { error = "Seniority must be \"junior\" or \"experienced\"." });

    var resume = await db.UserResumes.FindAsync(userId);
    if (resume is null) return ResumeNotReadyError();

    resume.SectionConfigJson = JsonSerializer.Serialize(template.OrderFor(seniority.Value));
    resume.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return ResumeResponse(resume);
});

// POST /api/v1/resume/generate-summary — auto-generates a fresh Summary from BACKGROUND + target
// job titles alone (no job posting in context — that's CvTailorAgent's job, not this one). Draft
// only: does not write UserResume, same "nothing persists until Save" model as the rest of this
// page. Only needs UserProfile (not UserResume) to exist — the frontend only reaches this button
// once /resume has already loaded successfully, and this endpoint never touches the UserResume row.
api.MapPost("/resume/generate-summary", async (HttpContext ctx, AppDbContext db, ResumeSummaryAgent summaryAgent) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    var profile = await db.UserProfiles.FindAsync(userId);
    if (profile is null) return Results.NotFound();

    var background = BackgroundYamlParser.Parse(profile.Background);
    if (ResumeSummaryAgent.IsBackgroundEssentiallyEmpty(background))
        return Results.Json(
            new { error = "Add some experience, education, or projects to your Background first — there's nothing to summarize yet." },
            statusCode: StatusCodes.Status422UnprocessableEntity);

    var targetJobTitles = TargetJobTitles.Parse(profile.JobCriteria);
    var summary = await summaryAgent.GenerateAsync(userId, profile.Background, targetJobTitles);
    return Results.Ok(new { summary });
});

// GET /api/v1/sources — Tier 2 only. Source catalog, the user's current selection, and
// whether Gmail is already connected (so the frontend can hide the Connect Gmail button
// instead of inviting a pointless re-consent).
api.MapGet("/sources", async (HttpContext ctx, AppDbContext db) =>
{
    var (user, error) = await RequireTier2Async(db, CurrentUserId(ctx, UserIdClaimType));
    if (error is not null) return error;

    var catalog = JobSource.Catalog.Select(c => new { key = c.Key, label = c.Label, automatic = c.Automatic });
    var enabled = user!.EnabledSources?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];
    // Existence check only — no need to decrypt the token just to know it's there.
    var gmailConnected = await db.UserSecrets.AnyAsync(s => s.UserId == user.Id && s.Key == UserSecretKey.GmailSettingsRefreshToken);
    var gmailReadonlyConnected = await db.UserSecrets.AnyAsync(s => s.UserId == user.Id && s.Key == UserSecretKey.GmailRefreshToken);
    return Results.Ok(new { catalog, enabled, gmailConnected, gmailReadonlyConnected, gmailTrackingMode = user.GmailTrackingMode });
});

// PUT /api/v1/sources — body: { sources: string[] }. Unknown keys are dropped silently
// rather than rejected, so an older frontend build never hard-fails against a trimmed catalog.
api.MapPut("/sources", async (HttpContext ctx, SourcesUpdateRequest body, AppDbContext db) =>
{
    var (user, error) = await RequireTier2Async(db, CurrentUserId(ctx, UserIdClaimType));
    if (error is not null) return error;

    // A null sources field binds at runtime despite the DTO's non-nullable array param —
    // JobSource.Sanitize would otherwise throw on the null IEnumerable instead of a clean 400.
    if (body.Sources is null)
        return Results.BadRequest(new { error = "sources is required." });

    var sanitized = JobSource.Sanitize(body.Sources);
    user!.EnabledSources = string.Join(',', sanitized);
    await db.SaveChangesAsync();
    return Results.Ok(new { enabled = sanitized });
});

// PUT /api/v1/gmail-tracking-mode — body: { mode: "full"|"filter"|"manual" }. Deliberately no
// default is ever assumed server-side either — this only ever sets what the user explicitly
// picked. "full" still requires the user to separately grant readonly access (see
// /gmail-oauth/start?mode=full) — picking this mode alone doesn't request any new scope.
api.MapPut("/gmail-tracking-mode", async (HttpContext ctx, GmailTrackingModeRequest body, AppDbContext db) =>
{
    var (user, error) = await RequireTier2Async(db, CurrentUserId(ctx, UserIdClaimType));
    if (error is not null) return error;

    if (!GmailTrackingMode.All.Contains(body.Mode))
        return Results.BadRequest(new { error = "Invalid mode" });

    user!.GmailTrackingMode = body.Mode;
    await db.SaveChangesAsync();
    return Results.Ok(new { gmailTrackingMode = user.GmailTrackingMode });
});

// GET /api/v1/inbound-email — Tier 2 only. The user's opaque SendGrid forwarding address,
// generated on first request. 503 if the domain isn't configured yet (external SendGrid/DNS
// setup not finished) — see the SENDGRID_INBOUND_DOMAIN comment above.
api.MapGet("/inbound-email", async (HttpContext ctx, AppDbContext db) =>
{
    var (user, error) = await RequireTier2Async(db, CurrentUserId(ctx, UserIdClaimType));
    if (error is not null) return error;
    if (sendGridInboundDomain is null)
        return Results.Json(new { error = "Inbound email forwarding isn't set up yet." }, statusCode: StatusCodes.Status503ServiceUnavailable);

    var address = await InboundEmailService.GetOrCreateAddressAsync(db, user!.Id, sendGridInboundDomain);
    return Results.Ok(new { address });
});

// GET /api/v1/gmail-forwarding-status — Tier 2 only, Gmail must already be connected.
// Gmail's forwardingAddresses.create is restricted to domain-wide-delegated service
// accounts and doesn't work for a personal account (confirmed against Google's own docs),
// so the user has to add and confirm the forwarding address themselves in Gmail's own
// settings — this endpoint's job is to read that status back, and once it's verified,
// install the actual filter (GmailSettingsClient.EnsureJobAlertFilterAsync is idempotent,
// safe to call on every poll). 400 if Gmail isn't connected yet, 503 if the app's Gmail
// client isn't configured or the inbound domain isn't set up.
api.MapGet("/gmail-forwarding-status", async (HttpContext ctx, AppDbContext db, UserSecretService secrets) =>
{
    var (user, error) = await RequireTier2Async(db, CurrentUserId(ctx, UserIdClaimType));
    if (error is not null) return error;

    var gmailSettings = ctx.RequestServices.GetService<GmailSettingsClient>();
    if (gmailSettings is null || sendGridInboundDomain is null)
        return Results.Json(new { error = "Gmail forwarding isn't set up yet." }, statusCode: StatusCodes.Status503ServiceUnavailable);

    var refreshToken = await secrets.GetAsync(db, user!.Id, UserSecretKey.GmailSettingsRefreshToken);
    if (refreshToken is null)
        return Results.BadRequest(new { error = "Connect Gmail first." });

    var address = await InboundEmailService.GetOrCreateAddressAsync(db, user.Id, sendGridInboundDomain);

    string status;
    try
    {
        status = await gmailSettings.GetForwardingStatusAsync(refreshToken, address);
    }
    catch (TokenResponseException)
    {
        // Google invalidates the stored refresh token if the user revokes access in their
        // Google Account or it simply goes stale — an expected condition, not a server error.
        return Results.BadRequest(new { error = "Gmail access has expired or been revoked. Please reconnect Gmail." });
    }

    bool filterInstalled = false;
    if (status == GmailForwardingStatus.Verified)
    {
        try
        {
            // Return value is whether a *new* filter was just created — either way (already
            // existed or just created), the filter now exists.
            await gmailSettings.EnsureJobAlertFilterAsync(refreshToken, address);
            // Separate filter, same address — catches application-acknowledgment mail
            // (ATS/platform domains + phrase matches) so filter tracking mode can follow
            // applications through to acknowledgment without requiring full inbox access.
            await gmailSettings.EnsureAcknowledgmentFilterAsync(refreshToken, address);
            filterInstalled = true;
        }
        catch (Exception ex)
        {
            // Don't let a filter-install hiccup hide an already-successful verification —
            // the address status itself is still accurate and worth returning regardless.
            await Console.Error.WriteLineAsync($"Filter install failed for user {user.Id}: {ex}");
        }
    }

    return Results.Ok(new { address, status, filterInstalled });
});

// GET /api/v1/gmail-oauth/start — Tier 2 only. Redirects to Google's consent screen. Runs
// while already signed in via the cookie session — not a login flow, a second, narrower
// connection on top of it. 503 if the app's own Gmail OAuth client isn't configured yet
// (see the GMAIL_CLIENT_ID comment above).
//
// ?mode=full requests gmail.readonly (application-tracking full-access mode) instead of the
// default gmail.settings.basic (filter mode, and the alert-forwarding flow that predates
// tracking-mode choice — absent/unrecognized mode keeps that original behavior unchanged).
// The mode travels to /gmail-oauth/callback via a second short-lived cookie, the same
// pattern as the "state" CSRF cookie below — Google's redirect back only carries "state"
// and "code", not any arbitrary query param we'd want to pass through ourselves.
api.MapGet("/gmail-oauth/start", async (HttpContext ctx, AppDbContext db, string? mode) =>
{
    var (_, error) = await RequireTier2Async(db, CurrentUserId(ctx, UserIdClaimType));
    if (error is not null) return error;

    var gmailOAuth = ctx.RequestServices.GetService<GmailOAuthService>();
    if (gmailOAuth is null)
        return Results.Json(new { error = "Gmail connection isn't set up yet." }, statusCode: StatusCodes.Status503ServiceUnavailable);

    bool full = mode == GmailTrackingMode.Full;
    var scope = full ? GmailOAuthService.ReadonlyScope : GmailOAuthService.SettingsBasicScope;

    // CSRF protection: a random nonce round-tripped through a short-lived cookie, checked
    // against the "state" Google echoes back to /gmail-oauth/callback below.
    var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    var cookieOptions = new CookieOptions
    {
        HttpOnly = true,
        Secure = !isDev,
        SameSite = isDev ? SameSiteMode.Lax : SameSiteMode.None,
        MaxAge = TimeSpan.FromMinutes(10),
    };
    ctx.Response.Cookies.Append("gmail_oauth_state", state, cookieOptions);
    ctx.Response.Cookies.Append("gmail_oauth_mode", full ? GmailTrackingMode.Full : GmailTrackingMode.Filter, cookieOptions);

    return Results.Redirect(gmailOAuth.BuildAuthorizationUrl(state, scope));
});

// GET /api/v1/gmail-oauth/callback — Google redirects the browser here after consent (or
// denial). Exchanges the code for a refresh token, stores it encrypted, and sends the
// browser back to the frontend's sources page either way — success or failure is signalled
// via a query param there rather than an API-shaped JSON error, since this response goes
// straight to the browser's address bar, not a fetch() caller.
api.MapGet("/gmail-oauth/callback", async (HttpContext ctx, AppDbContext db, UserSecretService secrets) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    // FRONTEND_URL is configured with a trailing slash in Railway (used standalone
    // elsewhere, e.g. /auth/login's redirect, where that's harmless) — trimmed here since
    // this is the only place a path gets appended onto it, or "https://x.com/" + "/sources"
    // produces a double slash that fails React Router's path matching entirely.
    var redirectBase = (frontendUrl ?? "http://localhost:5173").TrimEnd('/');

    var expectedState = ctx.Request.Cookies["gmail_oauth_state"];
    ctx.Response.Cookies.Delete("gmail_oauth_state");
    // Default "filter" preserves behavior for the original alert-forwarding flow, which
    // never sent ?mode= at /start and so never set this cookie either.
    bool full = ctx.Request.Cookies["gmail_oauth_mode"] == GmailTrackingMode.Full;
    ctx.Response.Cookies.Delete("gmail_oauth_mode");

    var code = ctx.Request.Query["code"].FirstOrDefault();
    var state = ctx.Request.Query["state"].FirstOrDefault();
    var deniedOrErrored = ctx.Request.Query["error"].FirstOrDefault() is not null;

    if (deniedOrErrored || code is null || expectedState is null || state != expectedState)
        return Results.Redirect($"{redirectBase}/sources?gmail=error");

    var gmailOAuth = ctx.RequestServices.GetService<GmailOAuthService>();
    if (gmailOAuth is null)
        return Results.Redirect($"{redirectBase}/sources?gmail=error");

    try
    {
        var refreshToken = await gmailOAuth.ExchangeCodeForRefreshTokenAsync(code);
        var key = full ? UserSecretKey.GmailRefreshToken : UserSecretKey.GmailSettingsRefreshToken;
        await secrets.SetAsync(db, userId, key, refreshToken);

        // A successful full-access grant is the mode confirmation itself for that path —
        // filter mode is instead set explicitly via PUT /gmail-tracking-mode, since
        // connecting Gmail for filters doesn't by itself mean the user chose that mode
        // (e.g. it's also needed for plain alert forwarding, independent of tracking mode).
        if (full)
        {
            var user = await db.Users.FindAsync(userId);
            if (user is not null)
            {
                user.GmailTrackingMode = GmailTrackingMode.Full;
                await db.SaveChangesAsync();
            }
        }
    }
    catch (Exception ex)
    {
        await Console.Error.WriteLineAsync($"Gmail OAuth token exchange failed for user {userId}: {ex}");
        return Results.Redirect($"{redirectBase}/sources?gmail=error");
    }

    return Results.Redirect($"{redirectBase}/sources?gmail=connected");
});

// POST /api/v1/account/upgrade-to-tier2 — self-serve, no payment gate. Beta-only mechanism
// (see TierUpgradeService); the frontend hard-redirects to /sources afterward, same pattern
// as /account/cancel below, since a tier change needs a fresh /auth/me fetch to take effect
// and useMe() only fetches once on mount.
api.MapPost("/account/upgrade-to-tier2", async (HttpContext ctx, AppDbContext db) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    var upgraded = await TierUpgradeService.UpgradeToTier2Async(db, userId);
    return upgraded ? Results.Ok() : Results.BadRequest(new { error = "Already Tier 2." });
});

// POST /api/v1/account/downgrade-to-tier1 — self-serve, mirrors upgrade-to-tier2 above.
// Forfeits any unused credit balance immediately (see TierDowngradeService) rather than
// leaving a Tier2-sized balance on a Tier1 account — the frontend confirm copy makes this
// explicit before the user commits.
api.MapPost("/account/downgrade-to-tier1", async (HttpContext ctx, AppDbContext db) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    var downgraded = await TierDowngradeService.DowngradeToTier1Async(db, userId);
    return downgraded ? Results.Ok() : Results.BadRequest(new { error = "Already Tier 1." });
});

// POST /api/v1/admin/invite — owner only. Adds the email to BetaInvite (idempotent — a
// repeat invite is a no-op, not a duplicate row) so it can sign in at all, landing straight
// at Tier 2. Still adds the invite even if SendGrid Mail Send isn't configured or the send
// fails — being able to invite people isn't blocked on that, the owner just has to tell them
// some other way in the meantime (emailSent reports which happened).
api.MapPost("/admin/invite", async (HttpContext ctx, InviteRequest body, AppDbContext db) =>
{
    var (_, error) = await RequireOwnerAsync(db, CurrentUserId(ctx, UserIdClaimType));
    if (error is not null) return error;

    var email = body.Email.Trim().ToLowerInvariant();
    if (string.IsNullOrEmpty(email) || !email.Contains('@'))
        return Results.BadRequest(new { error = "Invalid email." });

    if (!await db.BetaInvites.AnyAsync(i => i.Email == email))
    {
        db.BetaInvites.Add(new BetaInvite { Email = email, InvitedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    var emailSender = ctx.RequestServices.GetService<SendGridEmailService>();
    bool emailSent = false;
    if (emailSender is not null)
    {
        try
        {
            await emailSender.SendAsync(email, "You're invited to Work Santa",
                $"You've been invited to Work Santa. Sign in with this Google account at " +
                $"{frontendUrl ?? "the app"} to get started. You'll have full Tier 2 access right away.");
            emailSent = true;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Failed to send invite email to {email}: {ex}");
        }
    }

    return Results.Ok(new { email, emailSent });
});

// POST /api/v1/admin/users/{id}/backfill-resume — owner only. One-time, per-user migration from
// the old free-text CvBase to the new structured UserResume (resume-builder Phase 1). Idempotent:
// a UserResume row already existing for this user IS the "already migrated" signal, no separate
// flag needed (see UserResume.cs). Doesn't touch CvBase/Background — additive only, so a failed
// or skipped migration never regresses the still-live legacy generation path.
//
// Warnings, not a hard failure: real content loss is possible if the old CvBase has a section
// with no clean structured equivalent (confirmed against real data during Phase 1 planning — see
// the Phase 1 plan's note on Skills/Projects needing override fields at all). Rather than trying
// to detect every such case inside the agent, this diffs the old CvBase's actual `##` headings
// against what the freshly-migrated data renders — any heading that would silently vanish is
// surfaced here for manual review before Deploy B, not silently dropped.
api.MapPost("/admin/users/{id:int}/backfill-resume", async (int id, HttpContext ctx, AppDbContext db, ResumeBackfillAgent backfillAgent) =>
{
    var (_, error) = await RequireOwnerAsync(db, CurrentUserId(ctx, UserIdClaimType));
    if (error is not null) return error;

    var profile = await db.UserProfiles.FindAsync(id);
    if (profile is null) return Results.NotFound();

    if (await db.UserResumes.FindAsync(id) is not null)
        return Results.Ok(new { userId = id, alreadyMigrated = true });

    var result = await backfillAgent.BackfillAsync(id, profile.Background, profile.CvBase);
    var resume = BuildUserResume(id, result);

    var background = BackgroundYamlParser.Parse(profile.Background);
    var rendered = ResumeRenderer.Render(background, resume);
    var warnings = MarkdownHeadings(profile.CvBase).Except(MarkdownHeadings(rendered), StringComparer.OrdinalIgnoreCase)
        .Select(h => $"CvBase had a \"{h}\" section that the migrated data doesn't render — check manually.")
        .ToList();

    db.UserResumes.Add(resume);
    await db.SaveChangesAsync();

    return Results.Ok(new { userId = id, alreadyMigrated = false, warnings });
});

// POST /api/v1/account/cancel — body: { deleteData: bool }. Soft-deactivates the account
// (blocks future login) and signs out the current session immediately. Doesn't revoke any
// other active session for this account elsewhere — there's no server-side session store to
// revoke against, only the signed cookie — so a second open tab stays signed in until it
// expires or the cookie is cleared there too. Acceptable for now: the login check still
// blocks any future sign-in attempt regardless.
//
// Gmail access always ends here, unconditionally: the refresh token is revoked with Google
// directly (not just dropped locally — a cancelled account shouldn't leave a grant standing
// in the user's Google Account permissions) and deleted. There's no reason for a deactivated
// account to keep a live grant just because the user wants their history preserved —
// reconnecting Gmail is a normal, expected part of coming back anyway.
//
// deleteData is the user's own explicit choice, not a default we pick for them: RawEmails,
// Classifications (the raw inbox-derived content) and Applications (the tracked history
// derived from it) are all deleted only if they asked for that, so someone planning to
// return can keep their tracked application history intact rather than starting over.
// This matches the Settings UI copy ("Removes your tracked application history and
// everything derived from your inbox") — Applications used to be deliberately spared here,
// but that left real data behind despite the UI promising it would go, so it's now scoped
// by the same deleteData flag as the other two tables. ApplicationEvents cascade-delete at
// the DB level (see AppDbContext's Application -> ApplicationEvent OnDelete(Cascade)), so
// they don't need a separate RemoveRange here.
api.MapPost("/account/cancel", async (HttpContext ctx, AppDbContext db, UserSecretService secrets, CancelAccountRequest body) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    var user = await db.Users.FindAsync(userId);
    if (user is null) return Results.NotFound();

    var gmailOAuth = ctx.RequestServices.GetService<GmailOAuthService>();
    foreach (var key in new[] { UserSecretKey.GmailRefreshToken, UserSecretKey.GmailSettingsRefreshToken })
    {
        var token = await secrets.GetAsync(db, userId, key);
        if (token is null) continue;

        if (gmailOAuth is not null)
        {
            try
            {
                await gmailOAuth.RevokeAsync(token);
            }
            catch (Exception ex)
            {
                // Best-effort: Google being briefly unreachable shouldn't block the user from
                // cancelling their own account. The token is deleted from our side either way,
                // so we stop using it regardless of whether Google's revoke call succeeded.
                await Console.Error.WriteLineAsync($"Gmail token revoke failed for user {userId}: {ex}");
            }
        }
        await secrets.DeleteAsync(db, userId, key);
    }

    if (body.DeleteData)
    {
        var rawEmails = await db.RawEmails.Where(r => r.UserId == userId).ToListAsync();
        db.RawEmails.RemoveRange(rawEmails);
        var classifications = await db.Classifications.Where(c => c.UserId == userId).ToListAsync();
        db.Classifications.RemoveRange(classifications);
        var applications = await db.Applications.Where(a => a.UserId == userId).ToListAsync();
        db.Applications.RemoveRange(applications);
    }

    user.DeactivatedAt = DateTime.UtcNow;
    db.AnalyticsEvents.Add(new AnalyticsEvent { UserId = userId, EventType = AnalyticsEventType.Cancellation, CreatedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    return Results.Ok();
});

// ---------------------------------------------------------------------------
// CV / cover letter / answer generation — authenticated web endpoints, backed by AgentThread.
// ---------------------------------------------------------------------------

// A pasted URL alone carries no title/company to search Jora/Adzuna with (unlike the Seek
// email-alert pipeline, which always has those from the alert itself) — postingHint covers
// that gap when the direct fetch fails.
// Search by title alone — Jora/Adzuna's keyword search ranks on job-title/skill tokens, and
// blending a company name into that same query dilutes it (confirmed: "software engineer
// codafication" failed to surface an actual Codafication listing that a plain "software
// engineer" search finds easily). Company, when given, is passed to Jora separately so it can
// page further looking for it (see JoraFetcher.SearchAsync), then used again to reorder the
// combined results — it's a much better fit for either of those than for a literal keyword.
static async Task<List<JobFeedItem>> SearchCandidatesAsync(JoraFetcher jora, AdzunaFetcher? adzuna, string title, string? company)
{
    var candidates = new List<JobFeedItem>(await jora.SearchAsync(title, "Melbourne", company));
    if (adzuna is not null)
        candidates.AddRange(await adzuna.SearchAsync(title, "melbourne", company));
    return JobFetcherUtils.RankByCompany(candidates, company);
}

// Same matching mechanism as JobAlertProcessor's cross-check, just user-supplied title/company
// instead of text pulled from an alert email. Used by /cv,/letter,/answer as a fast path —
// when confident, skips the manual "pick from search results" step (/postings/search-candidates
// below) entirely.
static async Task<JobFeedItem?> TryCrossCheckAsync(CrossCheckDeps deps, int userId, string title, string? company)
{
    var candidates = await SearchCandidatesAsync(deps.Jora, deps.Adzuna, title, company);
    if (candidates.Count == 0) return null;
    var targetContext = string.IsNullOrWhiteSpace(company) ? title : $"{title} at {company}";
    return await deps.Matcher.FindMatchAsync(userId, targetContext, candidates);
}

// GET /api/v1/postings/search-candidates?title=...&company=... — the manual counterpart to
// the automatic cross-check above: no Claude call, just the raw Jora/Adzuna search results,
// for the Generate UI to show as pickable suggestions when the auto-match isn't confident
// enough (or the user wants to search directly). Includes each candidate's own PostingText
// (built from the search result itself, same as the automatic cross-check's
// match.ToPostingText()) — picking one must use that directly rather than re-fetching
// candidate.Url, since a site that blocks the original link (the whole reason the user is
// searching) will just as readily block that URL too even though it points at the same listing.
api.MapGet("/postings/search-candidates", async (HttpContext ctx, string title, string? company, JoraFetcher joraFetcher) =>
{
    if (string.IsNullOrWhiteSpace(title))
        return Results.BadRequest(new { error = "title is required." });

    var candidates = await SearchCandidatesAsync(joraFetcher, ctx.RequestServices.GetService<AdzunaFetcher>(), title, company);

    return Results.Ok(new
    {
        candidates = candidates.Take(6).Select(c =>
            new { c.Title, c.Company, c.Location, c.Url, c.Source, PostingText = c.ToPostingText() }),
    });
});

// Resolves posting text from a pasted URL, an existing (per-user) DiscoveredPosting, or
// pasted text directly — in that priority order. Falls back to the cached evaluation summary
// if a DiscoveredPosting can't be re-fetched — the caller just retries the same POST with
// postingText set; no stateful correlation needed.
// Company is only free when discoveryId is used (DiscoveredPosting already has it from its
// own evaluation) — null in every other case, since pasting a URL/text never runs a full
// evaluation. GenerateArtifactAsync fills the gap with CompanyExtractorAgent when this comes
// back null, rather than this function always paying for that call even when it's redundant.
static async Task<(string? PostingText, string EvalJson, string? Company, string? Error)> ResolvePostingTextAsync(
    AppDbContext db, JobPostingFetcher fetcher, CrossCheckDeps crossCheck, int userId, int? discoveryId,
    string? postingText, string? postingUrl = null, string? postingTitle = null, string? postingCompany = null)
{
    if (postingText is not null)
        return (postingText, "{}", null, null);

    if (postingUrl is not null)
    {
        try
        {
            return (await fetcher.FetchAsync(postingUrl), "{}", null, null);
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(postingTitle))
            {
                var match = await TryCrossCheckAsync(crossCheck, userId, postingTitle, postingCompany);
                if (match is not null)
                    return (match.ToPostingText(), "{}", match.Company, null);
                return (null, "{}", null, $"Couldn't fetch that URL, and no confident match for \"{postingTitle}\" on Jora or Adzuna either. Paste the posting text instead.");
            }

            return (null, "{}", null, "Could not fetch that URL. This happens with Seek/Jora/LinkedIn links specifically. Add the job title (and company, if known) and we'll try finding it, or paste the posting text instead.");
        }
    }

    if (discoveryId is null)
        return (null, "{}", null, "Provide a discoveryId, postingUrl, or postingText.");

    // Explicit ownership check, defense-in-depth: AppDbContext's global query filter
    // (HasQueryFilter, OnModelCreating) already makes FindAsync return null for a row owned
    // by another user; this re-checks it explicitly at the call site too, same pattern as
    // every other FindAsync-by-id site in this file.
    var posting = await db.DiscoveredPostings.FindAsync(discoveryId.Value);
    if (posting is null || posting.UserId != userId)
        return (null, "{}", null, "Discovery not found.");

    string evalJson = posting.EvaluationJson ?? "{}";

    // Cached at discovery time, when the page was still reachable — preferred over re-fetching
    // both because it can't fail and because it's the same text the evaluation was based on.
    // Only rows discovered before PostingText existed fall through to the fetch below.
    if (!string.IsNullOrWhiteSpace(posting.PostingText))
        return (posting.PostingText, evalJson, posting.Company, null);

    try
    {
        var text = await fetcher.FetchAsync(posting.Url);
        return (text, evalJson, posting.Company, null);
    }
    catch
    {
        if (posting.EvaluationJson is not null)
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var ev = JsonSerializer.Deserialize<PostingEvaluation>(posting.EvaluationJson, opts);
            if (ev is not null) return (EvalFormatter.ToPostingContext(ev), evalJson, posting.Company, null);
        }
        return (null, evalJson, null, "Could not fetch the posting and no cached copy is available. Retry with postingText.");
    }
}

static int CurrentUserId(HttpContext ctx, string claimType)
{
    var value = ctx.User.FindFirstValue(claimType);
    if (value is null)
    {
        var claims = string.Join("; ", ctx.User.Claims.Select(c => $"{c.Type}={c.Value}"));
        Console.Error.WriteLine($"[AuthDiag] CurrentUserId: missing '{claimType}' claim on {ctx.Request.Path}, authenticated={ctx.User.Identity?.IsAuthenticated}, claims=[{claims}]");
    }
    return int.Parse(value!, CultureInfo.InvariantCulture);
}

// Shared by every Tier 2-only endpoint (sources, and the Gmail/SendGrid ones still to come).
static async Task<(User? User, IResult? Error)> RequireTier2Async(AppDbContext db, int userId)
{
    var user = await db.Users.FindAsync(userId);
    if (user is null) return (null, Results.NotFound());
    if (user.Tier != UserTier.Tier2)
        return (null, Results.Json(new { error = "Tier 2 only" }, statusCode: StatusCodes.Status403Forbidden));
    return (user, null);
}

// Not static, unlike RequireTier2Async above — needs to close over ownerEmail, which a local
// function can do regardless of where it's declared relative to where it's called — it's
// hoisted through the whole enclosing scope either way.
async Task<(User? User, IResult? Error)> RequireOwnerAsync(AppDbContext db, int userId)
{
    var user = await db.Users.FindAsync(userId);
    if (user is null) return (null, Results.NotFound());
    if (!string.Equals(user.Email, ownerEmail, StringComparison.OrdinalIgnoreCase))
        return (null, Results.Json(new { error = "Owner only" }, statusCode: StatusCodes.Status403Forbidden));
    return (user, null);
}

// The resume backfill's content-loss safety net — see its endpoint comment. Deliberately just
// "## " lines, not full markdown parsing: PdfRenderer's own supported subset is the same small
// set (#/##/###/bullets/bold/---), so a `##` heading is exactly the granularity a whole missing
// "section" would show up at.
static HashSet<string> MarkdownHeadings(string markdown) =>
    [.. markdown.ReplaceLineEndings("\n").Split('\n')
        .Where(l => l.StartsWith("## "))
        .Select(l => l[3..].Trim())];

// Shared by GET/PUT /resume and POST /resume/apply-template — the one response shape all four
// return. Overrides/SkillsSection are always present here (never omitted like the request's
// optional fields) since this always reflects the real, fully-populated stored row.
static IResult ResumeResponse(UserResume resume) => Results.Ok(new
{
    resume.Summary,
    sectionConfig = JsonSerializer.Deserialize<List<SectionConfigEntry>>(resume.SectionConfigJson) ?? [],
    experienceOverrides = JsonSerializer.Deserialize<List<ExperienceOverride>>(resume.ExperienceOverridesJson) ?? [],
    projectOverrides = JsonSerializer.Deserialize<List<ProjectOverride>>(resume.ProjectOverridesJson) ?? [],
    skillsSection = JsonSerializer.Deserialize<List<SkillsSectionEntry>>(resume.SkillsSectionJson) ?? [],
    resume.UpdatedAt,
});

// Same 409 message /cv already uses for the same condition (UserResume not seeded yet).
static IResult ResumeNotReadyError() => Results.Json(
    new { error = "Resume setup isn't finished yet — try again shortly, or contact support if this persists." },
    statusCode: StatusCodes.Status409Conflict);

// Shared by the admin backfill endpoint and PUT /profile's new-intake seeding — same mapping
// from a ResumeBackfillResult to the row that gets saved.
static UserResume BuildUserResume(int userId, ResumeBackfillResult result) => new()
{
    UserId = userId,
    Summary = result.Summary,
    SectionConfigJson = JsonSerializer.Serialize(result.SectionConfig),
    ExperienceOverridesJson = JsonSerializer.Serialize(result.ExperienceOverrides),
    SkillsSectionJson = JsonSerializer.Serialize(result.SkillsSection),
    ProjectOverridesJson = JsonSerializer.Serialize(result.ProjectOverrides),
    UpdatedAt = DateTime.UtcNow,
};

// Shared by /cv and /letter — identical shape (resolve posting → generate → save a Complete
// thread → spend a credit → return { threadId, text }), differing only in which agent
// generates and how the initial user turn is built.
// The real credit gate for every generation/revision endpoint — an atomic decrement
// immediately before the Claude call it pays for, not just an earlier HasCreditAsync check
// (see CreditService's class comment for why that alone isn't enough). Returns 402 without
// calling `action` if the decrement fails; refunds and rethrows if `action` itself throws,
// so a failed Claude call never permanently costs the user a credit.
static async Task<IResult> WithCreditAsync(AppDbContext db, int userId, Func<Task<IResult>> action)
{
    if (!await CreditService.SpendCreditAsync(db, userId))
        return Results.Json(new { error = "Insufficient credits" }, statusCode: StatusCodes.Status402PaymentRequired);

    try
    {
        return await action();
    }
    catch
    {
        await CreditService.RefundCreditAsync(db, userId);
        throw;
    }
}

// Refunds the credit WithCreditAsync already spent and returns a clean 422, without persisting
// anything. Used wherever a free-text writing agent's output fails validation instead of
// throwing — see CoverLetterOutputValidator's own comment for why that needs a separate check
// rather than relying on WithCreditAsync's exception-based refund.
static async Task<IResult> InvalidOutputResult(AppDbContext db, int userId, string error)
{
    await CreditService.RefundCreditAsync(db, userId);
    return Results.Json(new { error }, statusCode: StatusCodes.Status422UnprocessableEntity);
}

static async Task<IResult> GenerateArtifactAsync(
    AppDbContext db, JobPostingFetcher fetcher, CrossCheckDeps crossCheck, CompanyExtractorAgent companyExtractor,
    AccuracyVerifierAgent verifier, string sourceMaterial,
    int userId, int? discoveryId, string? postingText, string? postingUrl, string? postingTitle, string? postingCompany,
    string artifactType,
    Func<string, string, Task<string>> generate,
    Func<string, string, string> buildInitialUserContent,
    // Only /letter passes this — /cv's CvTailorAgent.GenerateAsync is a forced tool-use call
    // (see architecture-conventions.md), so a refusal there already surfaces as a thrown
    // exception WithCreditAsync refunds on, not a 200 response that needs a content check.
    Func<string, bool>? isValidOutput = null,
    string invalidOutputError = "Couldn't generate this document — try again, or add more detail to your profile.")
{
    var (resolvedText, evalJson, company, error) = await ResolvePostingTextAsync(
        db, fetcher, crossCheck, userId, discoveryId, postingText, postingUrl, postingTitle, postingCompany);
    if (resolvedText is null)
        return Results.BadRequest(new { error });

    // ResolvePostingTextAsync's try/catch only catches a fetch that *throws* — a bot-block page,
    // login wall, or near-empty redirect target comes back as a non-null 200 response, so it
    // sails past the check above and would otherwise go straight to the writing agent, which
    // (reasonably) responds with a prose apology instead of a CV/letter. That apology then gets
    // saved and returned as an ordinary successful generation with nothing to distinguish it —
    // see GenerateArtifactAsync's own comment. A real posting is always well over this floor.
    if (!PostingTextSufficiency.IsSufficient(resolvedText))
        return Results.BadRequest(new { error = "Could not fetch a usable posting from that URL. Try pasting the job description text instead." });

    // Only known for free via discoveryId (DiscoveredPosting already has it) — everywhere
    // else, a cheap dedicated extraction beats leaving the download filename generic.
    company ??= await companyExtractor.ExtractAsync(userId, resolvedText);

    return await WithCreditAsync(db, userId, async () =>
    {
        var text = await generate(resolvedText, evalJson);

        // A 200 response with prose isn't necessarily a valid artifact — a free-text writing
        // agent can hand back a refusal instead of the document itself, and that never throws.
        // Catch it here, before it's ever saved as a Complete thread or served via the PDF/Word
        // download endpoints.
        if (isValidOutput is not null && !isValidOutput(text))
            return await InvalidOutputResult(db, userId, invalidOutputError);

        // Non-blocking: a flagged claim doesn't stop the credit spend or hide the result, it
        // just gets surfaced for the user to review — see AccuracyVerifierAgent's own comment.
        var warnings = await verifier.VerifyAsync(userId, sourceMaterial, text);

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
            AccuracyWarningsJson = JsonSerializer.Serialize(warnings),
            Company = company,
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

        return Results.Ok(new { threadId = thread.Id, text, accuracyWarnings = warnings });
    });
}

// POST /api/v1/cv — body: { discoveryId?: int, postingText?: string, postingUrl?: string, postingTitle?: string, postingCompany?: string }
api.MapPost("/cv", async (HttpContext ctx, GenerateRequest body, AppDbContext db, JobPostingFetcher fetcher,
    JoraFetcher joraFetcher, PostingMatcherAgent matcher, CompanyExtractorAgent companyExtractor,
    AccuracyVerifierAgent verifier, CvTailorAgent cvAgent) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    if (!await CreditService.HasCreditAsync(db, userId))
        return Results.Json(new { error = "Insufficient credits" }, statusCode: StatusCodes.Status402PaymentRequired);

    var profile = await db.UserProfiles.FindAsync(userId)
        ?? throw new InvalidOperationException("UserProfile not found for the current user.");
    // Unlike UserProfile, not guaranteed to exist yet — it's created by the admin backfill or by
    // PUT /profile's best-effort seeding (which explicitly swallows failure). A real, if rare,
    // gap: surface it as a clear retryable error, not an NRE.
    var resume = await db.UserResumes.FindAsync(userId);
    if (resume is null)
        return Results.Json(new { error = "Resume setup isn't finished yet — try again shortly, or contact support if this persists." }, statusCode: StatusCodes.Status409Conflict);

    var crossCheck = new CrossCheckDeps(joraFetcher, ctx.RequestServices.GetService<AdzunaFetcher>(), matcher);
    // Matches CvTailorAgent.BuildSystemPrompt's own context exactly — verifying against
    // anything less (or more) would misrepresent what the generator actually had to work with.
    var sourceMaterial = $"{profile.Background}\n\n--- BASE CV ---\n{ResumeRenderer.Render(BackgroundYamlParser.Parse(profile.Background), resume, isPromptContext: true)}";
    return await GenerateArtifactAsync(db, fetcher, crossCheck, companyExtractor, verifier, sourceMaterial, userId,
        body.DiscoveryId, body.PostingText, body.PostingUrl, body.PostingTitle, body.PostingCompany,
        AgentThreadType.Cv,
        (text, evalJson) => cvAgent.GenerateAsync(profile, resume, text, evalJson),
        CvTailorAgent.BuildInitialUserContent);
}).RequireRateLimiting("generation");

// POST /api/v1/letter — body: { discoveryId?: int, postingText?: string, postingUrl?: string, postingTitle?: string, postingCompany?: string }
api.MapPost("/letter", async (HttpContext ctx, GenerateRequest body, AppDbContext db, JobPostingFetcher fetcher,
    JoraFetcher joraFetcher, PostingMatcherAgent matcher, CompanyExtractorAgent companyExtractor,
    AccuracyVerifierAgent verifier, CoverLetterAgent letterAgent) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    if (!await CreditService.HasCreditAsync(db, userId))
        return Results.Json(new { error = "Insufficient credits" }, statusCode: StatusCodes.Status402PaymentRequired);

    var profile = await db.UserProfiles.FindAsync(userId)
        ?? throw new InvalidOperationException("UserProfile not found for the current user.");

    var crossCheck = new CrossCheckDeps(joraFetcher, ctx.RequestServices.GetService<AdzunaFetcher>(), matcher);
    // CoverLetterAgent.BuildSystemPrompt only includes Background, not CvBase — same here.
    return await GenerateArtifactAsync(db, fetcher, crossCheck, companyExtractor, verifier, profile.Background, userId,
        body.DiscoveryId, body.PostingText, body.PostingUrl, body.PostingTitle, body.PostingCompany,
        AgentThreadType.CoverLetter,
        (text, evalJson) => letterAgent.GenerateAsync(profile, text, evalJson),
        CoverLetterAgent.BuildInitialUserContent,
        CoverLetterOutputValidator.LooksLikeCoverLetter,
        "Couldn't generate a cover letter — your background details may be too sparse for this role. Try adding more to your profile.");
}).RequireRateLimiting("generation");

// POST /api/v1/answer — body: { question: string, discoveryId?: int, postingUrl?: string, postingTitle?: string, postingCompany?: string }
api.MapPost("/answer", async (HttpContext ctx, AnswerRequest body, AppDbContext db, JobPostingFetcher fetcher,
    JoraFetcher joraFetcher, PostingMatcherAgent matcher, AnswerAgent answerAgent, AccuracyVerifierAgent verifier) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    if (!await CreditService.HasCreditAsync(db, userId))
        return Results.Json(new { error = "Insufficient credits" }, statusCode: StatusCodes.Status402PaymentRequired);

    string? jobContext = null;
    if (body.PostingUrl is not null)
    {
        try { jobContext = await fetcher.FetchAsync(body.PostingUrl); }
        catch
        {
            // No job context is fine — the question can still be answered generically. But
            // try the same cross-check /cv and /letter use first, if a title was given.
            if (!string.IsNullOrWhiteSpace(body.PostingTitle))
            {
                var crossCheck = new CrossCheckDeps(joraFetcher, ctx.RequestServices.GetService<AdzunaFetcher>(), matcher);
                var match = await TryCrossCheckAsync(crossCheck, userId, body.PostingTitle, body.PostingCompany);
                if (match is not null) jobContext = match.ToPostingText();
            }
        }
    }
    else if (body.DiscoveryId is int discoveryId)
    {
        // Explicit ownership check, defense-in-depth: AppDbContext's global query filter
        // (HasQueryFilter, OnModelCreating) already makes FindAsync return null for a row owned
        // by another user; this re-checks it explicitly at the call site too, same pattern as
        // every other FindAsync-by-id site in this file.
        var posting = await db.DiscoveredPostings.FindAsync(discoveryId);
        if (posting is not null && posting.UserId != userId)
            posting = null;
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
    return await WithCreditAsync(db, userId, async () =>
    {
        var (mode, content) = await answerAgent.RespondAsync(profile, history);
        history.Add(new AgentThreadTurn("assistant", content));

        // A follow-up question isn't a factual claim about the candidate — nothing to verify
        // until there's an actual final_answer.
        var warnings = mode == "final_answer"
            ? await verifier.VerifyAsync(userId, profile.Background, content)
            : [];

        var thread = new AgentThread
        {
            UserId = userId,
            ArtifactType = AgentThreadType.Answer,
            HistoryJson = JsonSerializer.Serialize(history),
            CurrentContent = mode == "final_answer" ? content : null,
            AccuracyWarningsJson = JsonSerializer.Serialize(warnings),
            Status = mode == "final_answer" ? AgentThreadStatus.Complete : AgentThreadStatus.AwaitingContext,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.AgentThreads.Add(thread);
        db.AnalyticsEvents.Add(new AnalyticsEvent { UserId = userId, EventType = AnalyticsEventType.AnswerGenerated, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        return Results.Ok(new { threadId = thread.Id, mode, content, accuracyWarnings = warnings });
    });
}).RequireRateLimiting("generation");

// POST /api/v1/threads/{id}/edit — body: { message: string }
// Dual-purpose: on an AwaitingContext (Answer) thread this continues the Q&A; on a Complete
// thread it's a revision request.
api.MapPost("/threads/{id:int}/edit", async (
    int id, HttpContext ctx, EditRequest body, AppDbContext db,
    CvTailorAgent cvAgent, CoverLetterAgent letterAgent, AnswerAgent answerAgent, AccuracyVerifierAgent verifier) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    // Explicit ownership check, defense-in-depth (see /applications/{id}/events above).
    var thread = await db.AgentThreads.FindAsync(id);
    if (thread is null || thread.UserId != userId) return Results.NotFound();

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

        return await WithCreditAsync(db, userId, async () =>
        {
            var (mode, content) = await answerAgent.RespondAsync(profile, history);
            history.Add(new AgentThreadTurn("assistant", content));

            var warnings = mode == "final_answer"
                ? await verifier.VerifyAsync(userId, profile.Background, content)
                : [];

            thread.HistoryJson = JsonSerializer.Serialize(history);
            thread.CurrentContent = mode == "final_answer" ? content : null;
            thread.AccuracyWarningsJson = JsonSerializer.Serialize(warnings);
            thread.Status = mode == "final_answer" ? AgentThreadStatus.Complete : AgentThreadStatus.AwaitingContext;
            thread.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new { threadId = thread.Id, mode, content, accuracyWarnings = warnings });
        });
    }

    history.Add(new AgentThreadTurn("user",
        $"Please revise the previous draft with this feedback: {body.Message}\n\n" +
        "Keep following all the rules in your instructions unless the feedback specifically asks to change them."));

    UserResume? resume = null;
    if (thread.ArtifactType == AgentThreadType.Cv)
    {
        resume = await db.UserResumes.FindAsync(userId);
        if (resume is null)
            return Results.Json(new { error = "Resume setup isn't finished yet — try again shortly, or contact support if this persists." }, statusCode: StatusCodes.Status409Conflict);
    }

    return await WithCreditAsync(db, userId, async () =>
    {
        string? text = null;
        string? answerMode = null;
        string? answerContent = null;

        if (thread.ArtifactType == AgentThreadType.Cv)
        {
            text = await cvAgent.ReviseAsync(profile, resume!, history);
            // Same free-text-refusal risk as CoverLetterAgent.ReviseAsync below — see
            // CvRevisionOutputValidator.
            if (!CvRevisionOutputValidator.LooksLikeRevisedResume(text))
                return await InvalidOutputResult(db, userId,
                    "Couldn't revise the CV — try rephrasing your feedback, or add more detail to your profile.");
        }
        else if (thread.ArtifactType == AgentThreadType.CoverLetter)
        {
            text = await letterAgent.ReviseAsync(profile, history);
            // Same free-text-refusal risk as the initial /letter generation — see
            // GenerateArtifactAsync/CoverLetterOutputValidator.
            if (!CoverLetterOutputValidator.LooksLikeCoverLetter(text))
                return await InvalidOutputResult(db, userId,
                    "Couldn't revise the cover letter — try rephrasing your feedback, or add more detail to your profile.");
        }
        else
            (answerMode, answerContent) = await answerAgent.RespondAsync(profile, history);

        var finalText = text ?? answerContent ?? "";
        history.Add(new AgentThreadTurn("assistant", finalText));

        // "Make it sound more impressive" is exactly the kind of revision request that could
        // introduce embellishment a first draft wouldn't have had — re-verify every revision,
        // not just the original generation. Skipped only for an Answer thread still awaiting
        // another follow-up round (answerMode set but not "final_answer" — nothing final to check).
        string[] warnings;
        if (answerMode is not null && answerMode != "final_answer")
        {
            warnings = [];
        }
        else
        {
            var sourceMaterial = thread.ArtifactType == AgentThreadType.Cv
                ? $"{profile.Background}\n\n--- BASE CV ---\n{ResumeRenderer.Render(BackgroundYamlParser.Parse(profile.Background), resume!, isPromptContext: true)}"
                : profile.Background;
            warnings = await verifier.VerifyAsync(userId, sourceMaterial, finalText);
        }

        thread.HistoryJson = JsonSerializer.Serialize(history);
        thread.CurrentContent = text ?? answerContent;
        thread.AccuracyWarningsJson = JsonSerializer.Serialize(warnings);
        thread.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Results.Ok(new { threadId = thread.Id, text, mode = answerMode, content = answerContent, accuracyWarnings = warnings });
    });
}).RequireRateLimiting("generation");

// "{Applicant} - {Company} - {DocType}.{ext}", gracefully dropping any segment that isn't
// known rather than leaving a blank gap (e.g. "Kavin Abeysinghe - Resume.pdf" when company
// couldn't be identified).
static string BuildDownloadFilename(string? applicantName, string? company, string docType, string ext)
{
    var parts = new[] { applicantName, company, docType }
        .Where(p => !string.IsNullOrWhiteSpace(p));
    var raw = string.Join(" - ", parts);
    var safe = Regex.Replace(raw, "[\\\\/:*?\"<>|]", "").Trim();
    return $"{safe}.{ext}";
}

// Reads straight from Background now, not CvBase's first markdown line — BackgroundYamlParser.
// Parse never throws (see its own doc comment), so a blank/null name just falls through to null,
// same as the old "no CvBase yet" case for a brand new user mid-onboarding.
static string? ExtractApplicantName(string? backgroundYaml)
{
    if (string.IsNullOrWhiteSpace(backgroundYaml)) return null;
    var name = BackgroundYamlParser.Parse(backgroundYaml).Personal.Name;
    return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
}

static async Task<string?> GetApplicantNameAsync(AppDbContext db, int userId)
{
    var profile = await db.UserProfiles.FindAsync(userId);
    return ExtractApplicantName(profile?.Background);
}

// GET /api/v1/threads/{id} — the thread's current persisted state, so the frontend can restore
// a just-generated CV/cover letter after an accidental refresh (see ThreadStateResponse).
api.MapGet("/threads/{id:int}", async (int id, HttpContext ctx, AppDbContext db) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    // Explicit ownership check, defense-in-depth (see /applications/{id}/events above).
    var thread = await db.AgentThreads.FindAsync(id);
    if (thread is null || thread.UserId != userId) return Results.NotFound();

    return Results.Ok(ThreadStateResponse.From(thread));
});

// GET /api/v1/threads/{id}/pdf — renders a completed CV or cover-letter thread as a PDF
api.MapGet("/threads/{id:int}/pdf", async (int id, HttpContext ctx, AppDbContext db) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    // Explicit ownership check, defense-in-depth (see /applications/{id}/events above).
    var thread = await db.AgentThreads.FindAsync(id);
    if (thread is null || thread.UserId != userId || thread.CurrentContent is null) return Results.NotFound();

    var applicantName = await GetApplicantNameAsync(db, thread.UserId);

    return thread.ArtifactType switch
    {
        AgentThreadType.Cv => Results.File(
            JobSearch.Api.Services.PdfRenderer.RenderCv(thread.CurrentContent), "application/pdf",
            BuildDownloadFilename(applicantName, thread.Company, "Resume", "pdf")),
        AgentThreadType.CoverLetter => Results.File(
            JobSearch.Api.Services.PdfRenderer.RenderLetter(thread.CurrentContent), "application/pdf",
            BuildDownloadFilename(applicantName, thread.Company, "Cover Letter", "pdf")),
        _ => Results.NotFound(),
    };
});

// GET /api/v1/threads/{id}/docx — cover letter as a Word document
api.MapGet("/threads/{id:int}/docx", async (int id, HttpContext ctx, AppDbContext db) =>
{
    int userId = CurrentUserId(ctx, UserIdClaimType);
    // Explicit ownership check, defense-in-depth (see /applications/{id}/events above).
    var thread = await db.AgentThreads.FindAsync(id);
    if (thread is null || thread.UserId != userId || thread.CurrentContent is null
        || thread.ArtifactType != AgentThreadType.CoverLetter)
        return Results.NotFound();

    var applicantName = await GetApplicantNameAsync(db, thread.UserId);

    var docx = JobSearch.Api.Services.WordRenderer.RenderLetter(thread.CurrentContent);
    return Results.File(docx,
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        BuildDownloadFilename(applicantName, thread.Company, "Cover Letter", "docx"));
});

// ---------------------------------------------------------------------------
// SendGrid inbound webhook — unauthenticated but verified by a shared secret query param.
// SendGrid's Inbound Parse POST can't be configured with custom headers, so a header-based
// secret isn't an option — the secret is appended to the Destination URL configured in
// SendGrid instead (?secret=...).
// ---------------------------------------------------------------------------
app.MapPost("/api/v1/sendgrid/inbound", async (
    HttpRequest request, AppDbContext db, IServiceScopeFactory scopeFactory, ILogger<Program> logger) =>
{
    // No secret configured yet (external SendGrid/DNS setup not finished) — reject
    // everything rather than accept mail with nothing to verify it against.
    if (sendGridInboundSecret is null
        || !string.Equals(request.Query["secret"], sendGridInboundSecret, StringComparison.Ordinal))
        return Results.Unauthorized();

    IFormCollection form;
    try
    {
        form = await request.ReadFormAsync();
    }
    catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
    {
        // SendGrid posts the full raw email as multipart/form-data, attachments included —
        // a forwarded message can legitimately exceed the shared 8MB Kestrel body limit set
        // near the top of this file. Nothing to retry, since a bigger message never fits;
        // ack it like the "no matching user" case below so SendGrid doesn't redeliver it.
        // Logged (not just silently dropped) so this failure mode stays observable in Sentry
        // instead of vanishing the moment it stops throwing.
        logger.LogWarning(ex,
            "SendGrid inbound webhook: forwarded email exceeded the {MaxBytes}-byte Kestrel body limit, dropped. ContentLength={ContentLength}",
            8_000_000, request.ContentLength);
        return Results.Ok();
    }
    var to = form["to"].ToString();
    var userId = await InboundEmailService.ResolveUserIdAsync(db, to);

    // No match — a stale/mistyped address, or something hitting the wildcard domain
    // directly rather than a real per-user address. 200 either way so SendGrid doesn't
    // retry; there's nothing to recover by retrying an address that will never resolve.
    if (userId is null) return Results.Ok();

    var from = form["from"].ToString();
    var subject = form["subject"].ToString();
    // SendGrid usually provides a plain-text part; fall back to raw HTML on the rare
    // message that only has one, rather than dropping the email entirely.
    var text = form["text"].ToString();
    if (string.IsNullOrEmpty(text)) text = form["html"].ToString();

    // Fire-and-forget: respond 200 immediately, insert in the background via a fresh
    // scope — the request-scoped `db` above is gone by the time this runs.
    _ = Task.Run(async () =>
    {
        using var scope = scopeFactory.CreateScope();
        var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        scopedDb.CurrentUserId = userId;
        scopedDb.RawEmails.Add(new RawEmailRecord
        {
            UserId = userId.Value,
            // SendGrid doesn't provide a stable message id — each webhook POST is treated
            // as a genuinely new message, so a fresh id per call is correct, not a gap.
            MessageId = Guid.NewGuid().ToString(),
            ThreadId = Guid.NewGuid().ToString(),
            FromAddress = from,
            Subject = subject,
            BodyText = text,
            ReceivedAt = DateTime.UtcNow,
        });
        await scopedDb.SaveChangesAsync();

        // Gmail's own "confirm this forwarding address" email — the target address is our
        // inbound webhook, not a mailbox anyone could actually click the link from, so the
        // app completes it server-side the moment the email arrives. See
        // GmailForwardingConfirmation's comment for why this is safe against a spoofed From.
        if (GmailForwardingConfirmation.TryExtractVerificationLink(from, text, out var verifyLink))
        {
            try
            {
                // The verify link itself only serves an HTML confirmation page (a form with
                // a "Confirm" submit button) — a GET alone never completes anything; the
                // confirmation only happens when that form is POSTed, same as a human
                // clicking the button. The form's action is "" (post back to the same page),
                // so submit to the final URL after any redirect, carrying cookies between the
                // two calls in case Google ties the follow-up POST to a cookie set on the GET.
                using var handler = new HttpClientHandler { CookieContainer = new System.Net.CookieContainer() };
                using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
                var getResponse = await http.GetAsync(verifyLink);
                var confirmUrl = getResponse.RequestMessage?.RequestUri ?? new Uri(verifyLink);
                var postResponse = await http.PostAsync(confirmUrl, new StringContent(""));
                Console.WriteLine(postResponse.IsSuccessStatusCode
                    ? $"Auto-confirmed Gmail forwarding for user {userId}."
                    : $"Gmail forwarding auto-confirm for user {userId} returned {postResponse.StatusCode}.");
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Gmail forwarding auto-confirm failed for user {userId}: {ex}");
            }
        }
    });

    return Results.Ok();
}).AllowAnonymous().RequireRateLimiting("webhook");

// ---------------------------------------------------------------------------
// Sentry webhook — unauthenticated but HMAC-verified. Relays a newly-created Sentry issue
// into a GitHub repository_dispatch, which starts the automated crash-fix workflow.
//
// This relay exists because Sentry can't call GitHub directly: repository_dispatch needs an
// Authorization header and Sentry's webhook config can't set one. Doing it here also means
// triage runs as cheap deterministic C# rather than as an agent prompt — see CrashTriage.
// ---------------------------------------------------------------------------
app.MapPost("/api/v1/sentry/webhook", async (HttpRequest request, AppDbContext db) =>
{
    // Raw bytes, not the parsed model: the signature covers the exact body Sentry sent, so
    // it has to be verified before anything is deserialized from it.
    using var ms = new MemoryStream();
    await request.Body.CopyToAsync(ms);
    var rawBody = ms.ToArray();

    if (!SentryWebhookVerifier.IsValid(
            request.Headers["sentry-hook-signature"].FirstOrDefault(), rawBody, sentryWebhookSecret))
        return Results.Unauthorized();

    // Only newly-created issues start a fix run. Sentry also delivers resolved/assigned/
    // ignored events on the same hook.
    if (request.Headers["sentry-hook-resource"].FirstOrDefault() != "issue")
        return Results.Ok();

    using var doc = JsonDocument.Parse(rawBody);
    var root = doc.RootElement;
    if (root.TryGetProperty("action", out var action) && action.GetString() != "created")
        return Results.Ok();

    if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("issue", out var issue))
        return Results.Ok();

    string? Str(string name) => issue.TryGetProperty(name, out var v) ? v.GetString() : null;

    var issueId = Str("id");
    if (string.IsNullOrEmpty(issueId)) return Results.Ok();

    var title = Str("title") ?? "";
    var permalink = Str("permalink") ?? "";
    var projectSlug = issue.TryGetProperty("project", out var proj) && proj.TryGetProperty("slug", out var slug)
        ? slug.GetString() ?? ""
        : "";

    var alreadyDispatched = await db.CrashTriageDispatches.AnyAsync(d => d.SentryIssueId == issueId);
    var since = DateTime.UtcNow.AddHours(-1);
    var recentCount = await db.CrashTriageDispatches.CountAsync(d => d.DispatchedAt >= since);

    var decision = CrashTriage.Evaluate(Str("level"), title, alreadyDispatched, recentCount, CrashFixHourlyCap);
    if (!decision.ShouldDispatch)
    {
        Console.WriteLine($"[CrashTriage] Skipped issue {issueId}: {decision.Reason}");
        return Results.Ok();
    }

    if (crashFixGitHubToken is null)
    {
        Console.WriteLine($"[CrashTriage] Would dispatch issue {issueId} but no GitHub token configured.");
        return Results.Ok();
    }

    // Dispatched BEFORE the dedup row is written — the first real run of this pipeline
    // recorded dedup first, and a GitHub-side failure (bad token, wrong repo) then silently
    // and permanently marked a never-actually-dispatched issue as handled, with no automatic
    // retry and only a bare "failed" boolean in the logs to diagnose it from. Dispatching
    // first means a failure is retryable on the next webhook redelivery. The residual risk —
    // two concurrent deliveries both dispatching before either's row lands — is bounded by
    // the workflow's own `concurrency: group: crash-fix-<issueId>` (crash-fix.yml), which
    // queues rather than races a second run onto the same branch.
    var dispatcher = new GitHubDispatcher(crashFixRepo, crashFixGitHubToken);
    var result = await dispatcher.DispatchCrashFixAsync(issueId, title, projectSlug, permalink);

    if (!result.Success)
    {
        Console.WriteLine($"[CrashTriage] Dispatch failed for issue {issueId}: HTTP {result.StatusCode} — {result.ResponseBody}");
        return Results.Ok();
    }

    db.CrashTriageDispatches.Add(new CrashTriageDispatch
    {
        SentryIssueId = issueId,
        Title = title,
        ProjectSlug = projectSlug,
        DispatchedAt = DateTime.UtcNow,
    });
    await db.SaveChangesAsync();
    Console.WriteLine($"[CrashTriage] Dispatched issue {issueId} → GitHub: ok");

    return Results.Ok();
}).AllowAnonymous().RequireRateLimiting("webhook");

// Unknown /api/* paths → 404 (prevents SPA fallback returning index.html for bad API calls)
app.Map("/api/{**rest}", () => Results.NotFound());

// SPA fallback — serves index.html for all non-API, non-asset paths (React Router routes)
app.MapFallbackToFile("index.html");

Console.WriteLine("[Startup] Calling app.RunAsync()...");
await app.RunAsync();
