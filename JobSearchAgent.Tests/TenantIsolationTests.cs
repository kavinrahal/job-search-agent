using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

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

    // TC02 — CurrentUserId unset (null) with no CrossTenantAccess opt-in now throws instead of
    // silently returning zero rows (see AppDbContext.CrossTenantAccess / GuardedSet — the throw
    // fires at db.Applications property access, before the query even runs).
    // A caller that forgets to set CurrentUserId used to get an easy-to-miss empty result; it now
    // fails loudly and immediately, so a bug like this can't quietly ship as "no data" in prod.
    [Fact]
    public void Query_CurrentUserIdNullNoCrossTenantOptIn_Throws()
    {
        var db = Db.Fresh();
        db.Applications.Add(new JobSearch.Data.Application { UserId = 1, Company = "Acme", RoleTitle = "Engineer", Status = ApplicationStatus.Applied, AppliedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.SaveChanges();

        db.CurrentUserId = null;

        Assert.Throws<InvalidOperationException>(() => db.Applications.ToList());
    }

    // TC02b — the explicit opt-in (CrossTenantAccess = true) restores the old "matches nothing"
    // behavior instead of throwing: CurrentUserId is still null, so the filter still can't match
    // any row by UserId. A genuine cross-tenant read additionally needs .IgnoreQueryFilters() at
    // the call site — this opt-in alone only silences the guard, it doesn't grant visibility into
    // other tenants' rows by itself (see AppDbContext.CrossTenantAccess).
    [Fact]
    public void Query_CurrentUserIdNullWithCrossTenantOptIn_ReturnsNoRowsWithoutThrowing()
    {
        var db = Db.Fresh();
        db.Applications.Add(new JobSearch.Data.Application { UserId = 1, Company = "Acme", RoleTitle = "Engineer", Status = ApplicationStatus.Applied, AppliedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.SaveChanges();

        db.CurrentUserId = null;
        db.CrossTenantAccess = true;

        Assert.Empty(db.Applications.ToList());
    }

    // TC02c — CrossTenantAccess is a null-CurrentUserId-only escape hatch, not a general
    // "ignore tenancy" switch: with CurrentUserId still set, it must not change normal
    // per-tenant filtering behavior at all (no regression for every real request/worker
    // context, none of which ever set this flag).
    [Fact]
    public void Query_CrossTenantAccessSetWithCurrentUserId_StillFiltersToOwnTenant()
    {
        var db = Db.Fresh();
        db.CrossTenantAccess = true;
        db.Applications.Add(new JobSearch.Data.Application { UserId = 1, Company = "Acme", RoleTitle = "Engineer", Status = ApplicationStatus.Applied, AppliedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.Applications.Add(new JobSearch.Data.Application { UserId = 2, Company = "Globex", RoleTitle = "Engineer", Status = ApplicationStatus.Applied, AppliedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.SaveChanges();

        Assert.Equal(["Acme"], db.Applications.ToList().Select(a => a.Company));
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

    // TC05 — every entity with a UserId property must either have a query filter configured in
    // AppDbContext.OnModelCreating, or be explicitly named below as an intentional exception
    // (with a reason — each also has its own doc comment at its HasQueryFilter/no-filter site).
    // Catches a future entity being added with a UserId column but no HasQueryFilter call: today
    // that's a silent, unfiltered cross-tenant leak (the opposite failure mode from the
    // null-CurrentUserId guard elsewhere in this file — this one is about a *missing* filter,
    // not a missing tenant id).
    [Fact]
    public void EveryUserIdEntity_HasQueryFilter_UnlessExplicitlyExempted()
    {
        var exemptFromFiltering = new HashSet<string>
        {
            nameof(UserProfile),           // PK-reuse (UserId is the PK); always looked up by an exact known UserId.
            nameof(UserResume),            // Same PK-reuse pattern/reasoning as UserProfile.
            nameof(UserSecret),            // Always looked up by an exact, explicitly-passed userId argument.
            nameof(UserVerificationToken), // Pre-auth lookup by token hash/exact UserId — no CurrentUserId exists yet.
            nameof(AnalyticsEvent),        // Cross-tenant aggregation by design (owner-only analytics endpoint).
            nameof(SupportMessage),        // Cross-tenant by design (owner views everyone's submissions).
        };

        using var db = Db.Fresh();
        var entitiesWithUserId = db.Model.GetEntityTypes()
            .Where(e => e.ClrType.GetProperty("UserId") is not null)
            .ToList();

        // Sanity check on the reflection itself — if this ever finds nothing, the test isn't
        // testing anything and would pass for the wrong reason.
        Assert.NotEmpty(entitiesWithUserId);

        var namesWithUserId = entitiesWithUserId.Select(e => e.ClrType.Name).ToHashSet();
        var staleExemptions = exemptFromFiltering.Where(n => !namesWithUserId.Contains(n)).ToList();
        Assert.True(staleExemptions.Count == 0,
            $"Exempted names no longer correspond to a UserId-bearing entity (stale entry, fix the list): {string.Join(", ", staleExemptions)}");

        var missingFilter = entitiesWithUserId
            .Where(e => e.GetQueryFilter() is null && !exemptFromFiltering.Contains(e.ClrType.Name))
            .Select(e => e.ClrType.Name)
            .ToList();

        Assert.True(missingFilter.Count == 0,
            "Entities with a UserId property but no HasQueryFilter and not in exemptFromFiltering: " +
            $"{string.Join(", ", missingFilter)}. Either add " +
            "HasQueryFilter(x => x.UserId == CurrentUserId) in AppDbContext.OnModelCreating (and route " +
            "its DbSet property through GuardedSet<T>() alongside the other seven), or add the entity " +
            "to exemptFromFiltering above with a comment explaining why not.");
    }
}
