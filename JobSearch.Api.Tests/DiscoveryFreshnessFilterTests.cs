using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Api.Tests;

// GET /api/v1/discoveries' freshness filter: a DiscoveredPosting evaluated before the user's
// CURRENT JobCriteria was saved is stale (it was scored against a since-superseded criteria —
// possibly the sparse/incomplete criteria a user was still filling in) and must not be served,
// even though the row itself is kept as harmless history. These tests exercise the exact
// `Where` clause Program.cs's /discoveries handler adds — see the comment there for why it's
// a direct LINQ expression rather than a call into a shared helper method (EF Core can't
// translate an arbitrary static method into SQL for the Npgsql provider).
public class DiscoveryFreshnessFilterTests
{
    private const int OwnerUserId = 1;

    private static AppDbContext FreshDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options)
        { CurrentUserId = OwnerUserId };

    private static IQueryable<DiscoveredPosting> ApplyFreshnessFilter(
        IQueryable<DiscoveredPosting> query, DateTime? jobCriteriaUpdatedAt) =>
        query.Where(d => d.EvaluatedAt != null
            && (jobCriteriaUpdatedAt == null || d.EvaluatedAt >= jobCriteriaUpdatedAt));

    // TC01 — A posting evaluated BEFORE the user's current JobCriteria was saved must not be
    // returned — the exact stale-match bug this filter exists to fix.
    [Fact]
    public async Task Filter_EvaluatedBeforeCriteriaUpdatedAt_Excluded()
    {
        await using var db = FreshDb();
        var criteriaUpdatedAt = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        db.DiscoveredPostings.Add(new DiscoveredPosting
        {
            UserId = OwnerUserId, Url = "https://example.com/stale", Source = "seek",
            Title = "Stale Match", Company = "Acme", Recommendation = "strong_match",
            DiscoveredAt = criteriaUpdatedAt.AddDays(-2), EvaluatedAt = criteriaUpdatedAt.AddDays(-1),
        });
        await db.SaveChangesAsync();

        var results = await ApplyFreshnessFilter(db.DiscoveredPostings, criteriaUpdatedAt).ToListAsync();

        Assert.Empty(results);
    }

    // TC02 — A posting evaluated AFTER the user's current JobCriteria was saved must be
    // returned.
    [Fact]
    public async Task Filter_EvaluatedAfterCriteriaUpdatedAt_Included()
    {
        await using var db = FreshDb();
        var criteriaUpdatedAt = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        db.DiscoveredPostings.Add(new DiscoveredPosting
        {
            UserId = OwnerUserId, Url = "https://example.com/fresh", Source = "seek",
            Title = "Fresh Match", Company = "Acme", Recommendation = "strong_match",
            DiscoveredAt = criteriaUpdatedAt.AddDays(1), EvaluatedAt = criteriaUpdatedAt.AddDays(1),
        });
        await db.SaveChangesAsync();

        var results = await ApplyFreshnessFilter(db.DiscoveredPostings, criteriaUpdatedAt).ToListAsync();

        Assert.Equal(["Fresh Match"], results.Select(d => d.Title));
    }

    // TC03 — Mixed set: only the fresh one survives, the stale one is dropped, proving the
    // filter discriminates correctly rather than passing or blocking everything.
    [Fact]
    public async Task Filter_MixedStaleAndFresh_OnlyFreshReturned()
    {
        await using var db = FreshDb();
        var criteriaUpdatedAt = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        db.DiscoveredPostings.Add(new DiscoveredPosting
        {
            UserId = OwnerUserId, Url = "https://example.com/stale", Source = "seek",
            Title = "Stale Match", Company = "Acme", Recommendation = "good_match",
            DiscoveredAt = criteriaUpdatedAt.AddDays(-2), EvaluatedAt = criteriaUpdatedAt.AddDays(-1),
        });
        db.DiscoveredPostings.Add(new DiscoveredPosting
        {
            UserId = OwnerUserId, Url = "https://example.com/fresh", Source = "seek",
            Title = "Fresh Match", Company = "Acme", Recommendation = "good_match",
            DiscoveredAt = criteriaUpdatedAt.AddDays(1), EvaluatedAt = criteriaUpdatedAt.AddDays(1),
        });
        await db.SaveChangesAsync();

        var results = await ApplyFreshnessFilter(db.DiscoveredPostings, criteriaUpdatedAt).ToListAsync();

        Assert.Equal(["Fresh Match"], results.Select(d => d.Title));
    }

    // TC04 — jobCriteriaUpdatedAt is null (account hasn't re-saved JobCriteria since this
    // column was introduced) → nothing is treated as stale; an old evaluation still shows.
    // This is the deliberate backward-compat behavior — see UserProfile.JobCriteriaUpdatedAt's
    // own comment for why there's no backfill migration for existing rows.
    [Fact]
    public async Task Filter_NullCriteriaUpdatedAt_NothingFilteredAsStale()
    {
        await using var db = FreshDb();
        db.DiscoveredPostings.Add(new DiscoveredPosting
        {
            UserId = OwnerUserId, Url = "https://example.com/old", Source = "seek",
            Title = "Old Match", Company = "Acme", Recommendation = "weak_match",
            DiscoveredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EvaluatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });
        await db.SaveChangesAsync();

        var results = await ApplyFreshnessFilter(db.DiscoveredPostings, null).ToListAsync();

        Assert.Equal(["Old Match"], results.Select(d => d.Title));
    }

    // TC05 — A posting with no EvaluatedAt at all (shouldn't normally reach this filter given
    // the Recommendation gate applied earlier in the same query, but defensively guarded) is
    // never returned regardless of jobCriteriaUpdatedAt.
    [Fact]
    public async Task Filter_NullEvaluatedAt_Excluded()
    {
        await using var db = FreshDb();
        db.DiscoveredPostings.Add(new DiscoveredPosting
        {
            UserId = OwnerUserId, Url = "https://example.com/unevaluated", Source = "seek",
            Title = "Unevaluated", Company = "Acme", Recommendation = null,
            DiscoveredAt = DateTime.UtcNow, EvaluatedAt = null,
        });
        await db.SaveChangesAsync();

        var results = await ApplyFreshnessFilter(db.DiscoveredPostings, null).ToListAsync();

        Assert.Empty(results);
    }

    // TC06 — Evaluated at EXACTLY jobCriteriaUpdatedAt (the boundary) must be included — the
    // real endpoint's `>=`, not `>`.
    [Fact]
    public async Task Filter_EvaluatedExactlyAtCriteriaUpdatedAt_Included()
    {
        await using var db = FreshDb();
        var criteriaUpdatedAt = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
        db.DiscoveredPostings.Add(new DiscoveredPosting
        {
            UserId = OwnerUserId, Url = "https://example.com/boundary", Source = "seek",
            Title = "Boundary Match", Company = "Acme", Recommendation = "strong_match",
            DiscoveredAt = criteriaUpdatedAt, EvaluatedAt = criteriaUpdatedAt,
        });
        await db.SaveChangesAsync();

        var results = await ApplyFreshnessFilter(db.DiscoveredPostings, criteriaUpdatedAt).ToListAsync();

        Assert.Equal(["Boundary Match"], results.Select(d => d.Title));
    }
}
