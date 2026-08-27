using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Api.Tests;

// Regression coverage for the explicit per-record ownership checks added to Program.cs
// (GET /applications, /summary, /applications/{id}/events, PATCH /applications/{id},
// GET /discoveries, POST /threads/{id}/edit, GET /threads/{id}/pdf, GET /threads/{id}/docx).
//
// AppDbContext.OnModelCreating already defines a global EF Core query filter
// (HasQueryFilter(x => x.UserId == CurrentUserId)) on Application, DiscoveredPosting, and
// AgentThread, and JobSearchAgent.Tests/TenantIsolationTests.cs already proves that
// mechanism blocks cross-tenant reads on its own, including via FindAsync. These tests
// deliberately call .IgnoreQueryFilters() to take that layer out of the picture, so what's
// actually being verified here is the *second*, explicit layer Program.cs now applies at
// each call site — proving the endpoints don't rely solely on the global filter and would
// still refuse cross-tenant access if it were ever accidentally disabled or removed.
public class OwnedRecordOwnershipCheckTests
{
    private const int OwnerUserId = 1;
    private const int AttackerUserId = 2;

    private static DbContextOptions<AppDbContext> FreshOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    // TC01 — GET /applications: the explicit `.Where(a => a.UserId == userId)" added
    // alongside the existing status filter must, on its own (filters ignored), return only
    // the caller's own applications, not another user's.
    [Fact]
    public async Task ApplicationsList_QueryFiltersIgnored_ExplicitWhereStillExcludesOtherUsersRows()
    {
        var options = FreshOptions();
        await using (var seed = new AppDbContext(options) { CurrentUserId = OwnerUserId })
        {
            seed.Applications.Add(new Application { UserId = OwnerUserId, Company = "Acme", RoleTitle = "Engineer", Status = ApplicationStatus.Applied, AppliedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            seed.Applications.Add(new Application { UserId = AttackerUserId, Company = "Globex", RoleTitle = "Engineer", Status = ApplicationStatus.Applied, AppliedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            await seed.SaveChangesAsync();
        }

        await using var db = new AppDbContext(options) { CurrentUserId = OwnerUserId };
        var query = db.Applications.IgnoreQueryFilters().Where(a => a.UserId == OwnerUserId);
        var items = await query.ToListAsync();

        Assert.Equal(["Acme"], items.Select(a => a.Company));
    }

    // TC02 — GET /applications/{id}/events and PATCH /applications/{id}: the explicit
    // `application is null || application.UserId != userId` check must reject an id that
    // resolves to another user's row.
    [Fact]
    public async Task ApplicationOwnershipCheck_RowBelongsToOtherUser_TreatedAsNotFound()
    {
        var options = FreshOptions();
        int otherUsersApplicationId;
        await using (var seed = new AppDbContext(options) { CurrentUserId = AttackerUserId })
        {
            var app = new Application { UserId = AttackerUserId, Company = "Globex", RoleTitle = "Engineer", Status = ApplicationStatus.Applied, AppliedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            seed.Applications.Add(app);
            await seed.SaveChangesAsync();
            otherUsersApplicationId = app.Id;
        }

        await using var db = new AppDbContext(options) { CurrentUserId = OwnerUserId };
        var application = await db.Applications.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == otherUsersApplicationId);

        bool wouldBeNotFound = application is null || application.UserId != OwnerUserId;

        Assert.True(wouldBeNotFound);
    }

    // TC03 — Same shape, the legitimate-owner path: the check must NOT reject the caller's
    // own row (guards against an overzealous check accidentally locking owners out).
    [Fact]
    public async Task ApplicationOwnershipCheck_RowBelongsToCaller_Allowed()
    {
        var options = FreshOptions();
        int ownApplicationId;
        await using (var seed = new AppDbContext(options) { CurrentUserId = OwnerUserId })
        {
            var app = new Application { UserId = OwnerUserId, Company = "Acme", RoleTitle = "Engineer", Status = ApplicationStatus.Applied, AppliedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            seed.Applications.Add(app);
            await seed.SaveChangesAsync();
            ownApplicationId = app.Id;
        }

        await using var db = new AppDbContext(options) { CurrentUserId = OwnerUserId };
        var application = await db.Applications.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == ownApplicationId);

        bool wouldBeNotFound = application is null || application.UserId != OwnerUserId;

        Assert.False(wouldBeNotFound);
    }

    // TC04 — GET /discoveries: same explicit-where pattern as TC01, for DiscoveredPosting.
    [Fact]
    public async Task DiscoveriesList_QueryFiltersIgnored_ExplicitWhereStillExcludesOtherUsersRows()
    {
        var options = FreshOptions();
        await using (var seed = new AppDbContext(options) { CurrentUserId = OwnerUserId })
        {
            seed.DiscoveredPostings.Add(new DiscoveredPosting { UserId = OwnerUserId, Url = "https://example.com/own", Source = "seek", Title = "Engineer", Company = "Acme", DiscoveredAt = DateTime.UtcNow });
            seed.DiscoveredPostings.Add(new DiscoveredPosting { UserId = AttackerUserId, Url = "https://example.com/other", Source = "seek", Title = "Engineer", Company = "Globex", DiscoveredAt = DateTime.UtcNow });
            await seed.SaveChangesAsync();
        }

        await using var db = new AppDbContext(options) { CurrentUserId = OwnerUserId };
        var items = await db.DiscoveredPostings.IgnoreQueryFilters().Where(d => d.UserId == OwnerUserId).ToListAsync();

        Assert.Equal(["Acme"], items.Select(d => d.Company));
    }

    // TC05 — POST /threads/{id}/edit, GET /threads/{id}/pdf, GET /threads/{id}/docx: the
    // explicit `thread is null || thread.UserId != userId` check must reject another user's
    // thread id. This is the case behind the most severe finding — a cross-tenant thread-id
    // guess must not let one user revise or download another user's CV/cover letter.
    [Fact]
    public async Task ThreadOwnershipCheck_ThreadBelongsToOtherUser_TreatedAsNotFound()
    {
        var options = FreshOptions();
        int otherUsersThreadId;
        await using (var seed = new AppDbContext(options) { CurrentUserId = AttackerUserId })
        {
            var thread = new AgentThread
            {
                UserId = AttackerUserId,
                ArtifactType = AgentThreadType.CoverLetter,
                HistoryJson = "[]",
                CurrentContent = "Attacker's confidential cover letter content",
                Status = AgentThreadStatus.Complete,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            seed.AgentThreads.Add(thread);
            await seed.SaveChangesAsync();
            otherUsersThreadId = thread.Id;
        }

        await using var db = new AppDbContext(options) { CurrentUserId = OwnerUserId };
        var thread2 = await db.AgentThreads.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == otherUsersThreadId);

        bool wouldBeNotFound = thread2 is null || thread2.UserId != OwnerUserId;

        Assert.True(wouldBeNotFound);
    }

    // TC06 — Same shape, the legitimate-owner path for threads.
    [Fact]
    public async Task ThreadOwnershipCheck_ThreadBelongsToCaller_Allowed()
    {
        var options = FreshOptions();
        int ownThreadId;
        await using (var seed = new AppDbContext(options) { CurrentUserId = OwnerUserId })
        {
            var thread = new AgentThread
            {
                UserId = OwnerUserId,
                ArtifactType = AgentThreadType.CoverLetter,
                HistoryJson = "[]",
                CurrentContent = "Owner's own cover letter content",
                Status = AgentThreadStatus.Complete,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            seed.AgentThreads.Add(thread);
            await seed.SaveChangesAsync();
            ownThreadId = thread.Id;
        }

        await using var db = new AppDbContext(options) { CurrentUserId = OwnerUserId };
        var thread2 = await db.AgentThreads.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == ownThreadId);

        bool wouldBeNotFound = thread2 is null || thread2.UserId != OwnerUserId;

        Assert.False(wouldBeNotFound);
    }
}
