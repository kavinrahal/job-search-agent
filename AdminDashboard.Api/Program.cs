using System.Threading.RateLimiting;
using AdminDashboard.Api;
using AdminDashboard.Api.Data;
using JobSearch.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT") ?? "5100";
builder.WebHost.UseUrls($"http://+:{port}");

var isDev = builder.Environment.IsDevelopment();

// ---------------------------------------------------------------------------
// Database — two configurable connections, not one. ReadDatabaseUrl backs every normal
// page/view; WriteDatabaseUrl backs only the Emergency action handlers. Both are plain
// AppDbContext instances registered under different keyed-DI keys (see AdminDbContextKeys) so
// swapping WriteDatabaseUrl to a real least-privilege write role later is an env-var change,
// not a code change — see AdminConnectionStringBuilder's own doc comment.
// ---------------------------------------------------------------------------
var readDatabaseUrl = builder.Configuration["ReadDatabaseUrl"]
    ?? throw new InvalidOperationException("ReadDatabaseUrl not set.");
var readConnectionString = AdminConnectionStringBuilder.Build(readDatabaseUrl, maxPoolSize: 10);
var writeConnectionString = AdminConnectionStringBuilder.ResolveWrite(
    builder.Configuration["WriteDatabaseUrl"], readConnectionString, maxPoolSize: 5);

// CrossTenantAccess = true: this app is an owner-only admin surface that is genuinely
// cross-tenant by design and never sets CurrentUserId. That opt-in only silences
// AppDbContext's "no tenant, no CrossTenantAccess" guard (see AppDbContext.CurrentUserId) — a
// query against a UserId-filtered table here still needs `.IgnoreQueryFilters()` at the call
// site to see every tenant's rows; without it, it still returns zero rows, just quietly.
builder.Services.AddKeyedScoped<AppDbContext>(AdminDbContextKeys.Read, (_, _) =>
    new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(readConnectionString).Options) { CrossTenantAccess = true });
builder.Services.AddKeyedScoped<AppDbContext>(AdminDbContextKeys.Write, (_, _) =>
    new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(writeConnectionString).Options) { CrossTenantAccess = true });

// ---------------------------------------------------------------------------
// Auth — one shared username/password pair (AdminPortalUsername/AdminPortalPassword), its own
// independent cookie scheme. No shared cookie, no shared Users table dependency, nothing
// borrowed from JobSearch.Api's auth setup: this has to keep working even when that app's
// session system doesn't.
// ---------------------------------------------------------------------------
builder.Services.AddAuthentication(AdminAuthConstants.Scheme)
    .AddCookie(AdminAuthConstants.Scheme, o =>
    {
        o.Cookie.Name = isDev ? "admin-session" : "__Host-admin-session";
        o.Cookie.HttpOnly = true;
        o.Cookie.SecurePolicy = isDev ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
        // Same-origin admin tool only (no separate frontend deployment like JobSearch.Web) —
        // Strict is the tightest option and there's no cross-site flow that needs looser.
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.LoginPath = "/Login";
        o.ExpireTimeSpan = TimeSpan.FromHours(12);
        o.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(o =>
{
    // Every page requires a session by default; Login opts out explicitly via
    // [AllowAnonymous] rather than every other page opting in — a new page added later is
    // locked down unless someone deliberately decides otherwise.
    o.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddRazorPages();

// ---------------------------------------------------------------------------
// Forwarded headers — Railway terminates TLS at its edge and proxies plain HTTP to the
// container, so without this the app sees every request as HTTP: antiforgery cookies get
// issued without Secure, UseHsts() below is a no-op (it only ever fires over what it thinks
// is HTTP), and auth-challenge redirects build http:// Location headers. KnownIPNetworks/
// KnownProxies must be cleared — Railway's edge proxy isn't a fixed/known IP, and the default
// (loopback-only) trust list silently drops the header otherwise. This is safe unconditionally
// here specifically because Railway's edge is the only network path into the container (no
// direct public IP reachable bypassing it) — same reasoning and same pattern as
// JobSearch.Api/Program.cs, which is deployed on the same platform.
// ---------------------------------------------------------------------------
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
// Rate limiting — /Login only. This single shared username/password pair gates every
// direct-DB-write break-glass action in the app (credit adjustments, tier changes,
// deactivation, worker-lock clearing, maintenance-mode/banner toggles), so unlimited
// credential guessing is a real risk. Same fixed-window, IP-partitioned shape as
// JobSearch.Api's "webhook"/"auth" policies, just tighter than that app's "auth" policy
// (10/min) given what this one specific credential guards.
// ---------------------------------------------------------------------------
builder.Services.AddRateLimiter(o =>
{
    o.OnRejected = (ctx, _) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        return ValueTask.CompletedTask;
    };

    o.AddPolicy("auth", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

// Must be first — rewrites scheme/IP from X-Forwarded-* before anything (HSTS, antiforgery,
// auth redirects) reads them.
app.UseForwardedHeaders();

if (!isDev)
{
    app.UseHsts();
}

// Security headers on every response. CSP matches what the app actually serves — same-origin
// stylesheet only (no CDN/webfonts), plus the inline `style="..."` attributes across several
// pages and the one inline <script> block in Emergency.cshtml, so script-src/style-src need
// 'unsafe-inline' rather than locking down further (nonces/hashes for every one of those would
// be a much larger refactor than this fix warrants). default-src 'self' still blocks every
// remote resource load, and frame-ancestors 'none' blocks this admin tool's break-glass actions
// from being framed/clickjacked from another origin — real hardening even with 'unsafe-inline'
// present. No user-supplied content is ever reflected into these pages unescaped (see the prior
// audit's XSS-escaping finding), so 'unsafe-inline' isn't opening an actual injection path here.
#pragma warning disable S7039 // 'unsafe-inline' is required by this page's real inline style/script usage, see comment above
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    ctx.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    ctx.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'");
    await next();
});
#pragma warning restore S7039

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// POST /Logout — not a Razor Page since it has no view of its own, just a session teardown.
app.MapPost("/Logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(AdminAuthConstants.Scheme);
    return Results.Redirect("/Login");
});

app.MapRazorPages();

await app.RunAsync();
