using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Api.Tests;

// Coverage for the exact tenant-guard interaction GET /api/v1/health hits. The endpoint is
// [AllowAnonymous] (UptimeRobot-style monitors call it without a session), which means the
// CurrentUserId-stamping middleware in Program.cs never runs for it — its scoped AppDbContext
// always has CurrentUserId == null. Applications is a GuardedSet<T> DbSet (see
// AppDbContext.GuardedSet), so touching it with CurrentUserId == null and no CrossTenantAccess
// opt-in throws InvalidOperationException instead of returning data — that turned this health
// check into a 500 for every anonymous caller. The fix mirrors this exact scenario against
// AppDbContext directly, the same way TenantIsolationTests covers the guard mechanism itself,
// since there is no WebApplicationFactory/HTTP-level test harness in this project to drive the
// minimal-API lambda directly.
public class HealthEndpointTests
{
    private static AppDbContext FreshDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Application NewApplication(int userId, string company) => new()
    {
        UserId = userId,
        Company = company,
        RoleTitle = "Engineer",
        Status = ApplicationStatus.Applied,
        AppliedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    // TC01 — Reproduces the bug: an anonymous request's AppDbContext (CurrentUserId == null,
    // CrossTenantAccess == false, exactly the state UptimeRobot's call left it in) throws when
    // the health handler used to just call db.Applications.Count() directly.
    [Fact]
    public void Applications_AnonymousRequestState_ThrowsWithoutCrossTenantOptIn()
    {
        using var db = FreshDb();
        db.CurrentUserId = null;

        Assert.Throws<InvalidOperationException>(() => db.Applications.Count());
    }

    // TC02 — The actual fix: opting in with CrossTenantAccess + IgnoreQueryFilters() (the same
    // escape hatch AdminDashboard.Api already uses for cross-tenant reads) no longer throws for
    // an anonymous caller, and correctly totals every tenant's rows rather than just silently
    // returning zero (which CrossTenantAccess alone, without IgnoreQueryFilters, would do).
    [Fact]
    public void Applications_AnonymousRequestStateWithHealthCheckOptIn_ReturnsTotalAcrossAllTenants()
    {
        using var db = FreshDb();
        db.CrossTenantAccess = true;
        db.Applications.Add(NewApplication(1, "Acme"));
        db.Applications.Add(NewApplication(2, "Globex"));
        db.SaveChanges();

        db.CurrentUserId = null;

        var total = db.Applications.IgnoreQueryFilters().Count();

        Assert.Equal(2, total);
    }
}
