using AdminDashboard.Api;
using AdminDashboard.Api.Data;
using JobSearch.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
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

builder.Services.AddKeyedScoped<AppDbContext>(AdminDbContextKeys.Read, (_, _) =>
    new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(readConnectionString).Options));
builder.Services.AddKeyedScoped<AppDbContext>(AdminDbContextKeys.Write, (_, _) =>
    new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(writeConnectionString).Options));

// ---------------------------------------------------------------------------
// Auth — one shared secret (AdminPortalSecret), its own independent cookie scheme. No shared
// cookie, no shared Users table dependency, nothing borrowed from JobSearch.Api's auth setup:
// this has to keep working even when that app's session system doesn't.
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

var app = builder.Build();

if (!isDev)
{
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

// POST /Logout — not a Razor Page since it has no view of its own, just a session teardown.
app.MapPost("/Logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(AdminAuthConstants.Scheme);
    return Results.Redirect("/Login");
});

app.MapRazorPages();

await app.RunAsync();
