using JobSearch.Data;

namespace JobSearchAgent.Tests;

// Direct AppDbContext-level coverage for the CurrentUserId / HasQueryFilter mechanism
// introduced for multi-tenancy. One representative entity (Application) is enough to prove
// the mechanism — the HasQueryFilter shape is identical across all 7 scoped entities, so
// repeating this per entity wouldn't catch anything a typo in one shared pattern wouldn't
// already be caught by here.
public class TenantIsolationTests
{
    // TC01 — Two tenants' rows in the same DB never leak into each other's queries.
    // Silent failure: a missing/wrong HasQueryFilter would return both users' rows mixed
    // together with no exception — exactly the cross-tenant leak this ticket exists to prevent.
    [Fact]
    public void Query_TwoTenantsInSameDb_OnlyActiveTenantsRowsReturned()
    {
        var db = Db.Fresh(); // CurrentUserId = 1
        db.Applications.Add(new JobSearch.Data.Application { UserId = 1, Company = "Acme", RoleTitle = "Engineer", Status = ApplicationStatus.Applied, AppliedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.Applications.Add(new JobSearch.Data.Application { UserId = 2, Company = "Globex", RoleTitle = "Engineer", Status = ApplicationStatus.Applied, AppliedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.SaveChanges();

        var asUser1 = db.Applications.ToList();
        db.CurrentUserId = 2;
        var asUser2 = db.Applications.ToList();

        Assert.Equal(["Acme"], asUser1.Select(a => a.Company));
        Assert.Equal(["Globex"], asUser2.Select(a => a.Company));
    }

    // TC02 — CurrentUserId unset (null) returns zero rows, not every tenant's data.
    // This is the deliberate fail-closed default (see AppDbContext.CurrentUserId) — a caller
    // that forgets to set it must get nothing back, not an accidental full-tenant leak.
    [Fact]
    public void Query_CurrentUserIdNull_ReturnsNoRows()
    {
        var db = Db.Fresh();
        db.Applications.Add(new JobSearch.Data.Application { UserId = 1, Company = "Acme", RoleTitle = "Engineer", Status = ApplicationStatus.Applied, AppliedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.SaveChanges();

        db.CurrentUserId = null;

        Assert.Empty(db.Applications.ToList());
    }

    // TC03 — FindAsync (not just LINQ Where queries) also respects the tenant filter, for a
    // context that doesn't already have the row in its local change tracker.
    // Silent failure: a direct-by-id lookup (e.g. the /applications/{id}/events endpoint)
    // must not let one tenant read another tenant's row by guessing an id, even though list
    // endpoints correctly stay scoped.
    // Note: FindAsync only re-queries the DB (and so only applies the filter) when the
    // entity isn't already tracked locally — using two contexts against the same underlying
    // database (via Db.Fresh(dbName)) is required to exercise that path; a single context
    // that just inserted the row would return it straight from its own tracker, bypassing
    // the filter entirely and giving a false pass/fail unrelated to tenant isolation.
    [Fact]
    public async Task FindAsync_EntityBelongsToOtherTenant_ReturnsNull()
    {
        var dbName = Guid.NewGuid().ToString();
        var writer = Db.Fresh(dbName);
        var other = new JobSearch.Data.Application { UserId = 2, Company = "Globex", RoleTitle = "Engineer", Status = ApplicationStatus.Applied, AppliedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        writer.Applications.Add(other);
        writer.SaveChanges();

        var reader = Db.Fresh(dbName); // CurrentUserId = 1, independent tracker
        var found = await reader.Applications.FindAsync(other.Id);

        Assert.Null(found);
    }

    // TC04 — Users itself has no query filter: login must be able to look up a user by email
    // regardless of CurrentUserId, since that lookup is how CurrentUserId gets set in the
    // first place. If someone "completes the pattern" by adding a filter to Users too, every
    // login breaks in a circular way — can't resolve your own user row without one.
    [Fact]
    public async Task Users_NotTenantFiltered_VisibleRegardlessOfCurrentUserId()
    {
        var db = Db.Fresh();
        db.CurrentUserId = null;

        await UserProvisioningService.GetOrCreateAsync(db, "someone@example.com");

        db.CurrentUserId = 999; // unrelated to any UserId
        Assert.Contains(db.Users, u => u.Email == "someone@example.com");
    }
}
