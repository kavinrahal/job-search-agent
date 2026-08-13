using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Api.Tests;

public class AnalyticsServiceTests
{
    private static readonly DateTime Now = new(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc);

    private static AppDbContext FreshDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<User> SeedUser(AppDbContext db, string tier)
    {
        var user = new User { Email = $"{Guid.NewGuid()}@example.com", Tier = tier, CreditBalance = 0, CreatedAt = Now };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static void SeedEvent(AppDbContext db, int userId, string eventType, DateTime createdAt) =>
        db.AnalyticsEvents.Add(new AnalyticsEvent { UserId = userId, EventType = eventType, CreatedAt = createdAt });

    // TC01 — Total user count reflects every user regardless of tier.
    [Fact]
    public async Task GetSummaryAsync_CountsAllUsersRegardlessOfTier()
    {
        using var db = FreshDb();
        await SeedUser(db, UserTier.Tier1);
        await SeedUser(db, UserTier.Tier2);

        var summary = await AnalyticsService.GetSummaryAsync(db, Now);

        Assert.Equal(2, summary.TotalUsers);
    }

    // TC02 — Tier breakdown groups users by tier with correct per-group counts.
    [Fact]
    public async Task GetSummaryAsync_TierBreakdown_GroupsByTierWithCorrectCounts()
    {
        using var db = FreshDb();
        await SeedUser(db, UserTier.Tier1);
        await SeedUser(db, UserTier.Tier1);
        await SeedUser(db, UserTier.Tier2);

        var summary = await AnalyticsService.GetSummaryAsync(db, Now);

        Assert.Equal(2, summary.TierBreakdown.Single(t => t.Tier == UserTier.Tier1).Count);
        Assert.Equal(1, summary.TierBreakdown.Single(t => t.Tier == UserTier.Tier2).Count);
    }

    // TC03 — Events older than 30 days are excluded from the event-count window.
    // Silent failure: a wrong comparison here would either show stale data forever or an
    // empty funnel forever, and nothing would throw to reveal it.
    [Fact]
    public async Task GetSummaryAsync_EventCounts_ExcludesEventsOlderThan30Days()
    {
        using var db = FreshDb();
        var user = await SeedUser(db, UserTier.Tier1);
        SeedEvent(db, user.Id, AnalyticsEventType.Signup, Now.AddDays(-31));
        await db.SaveChangesAsync();

        var summary = await AnalyticsService.GetSummaryAsync(db, Now);

        Assert.Empty(summary.EventCountsLast30Days);
    }

    // TC04 — Events within the last 30 days are counted, grouped by event type.
    [Fact]
    public async Task GetSummaryAsync_EventCounts_IncludesEventsWithinWindow()
    {
        using var db = FreshDb();
        var user = await SeedUser(db, UserTier.Tier1);
        SeedEvent(db, user.Id, AnalyticsEventType.CvGenerated, Now.AddDays(-1));
        SeedEvent(db, user.Id, AnalyticsEventType.CvGenerated, Now.AddDays(-2));
        SeedEvent(db, user.Id, AnalyticsEventType.LetterGenerated, Now.AddDays(-3));
        await db.SaveChangesAsync();

        var summary = await AnalyticsService.GetSummaryAsync(db, Now);

        Assert.Equal(2, summary.EventCountsLast30Days.Single(e => e.EventType == AnalyticsEventType.CvGenerated).Count);
        Assert.Equal(1, summary.EventCountsLast30Days.Single(e => e.EventType == AnalyticsEventType.LetterGenerated).Count);
    }

    // TC05 — Active-user count is per distinct user, not per event.
    // Silent failure: counting raw events instead of distinct users would silently inflate
    // this number every time one user generates multiple things in a week.
    [Fact]
    public async Task GetSummaryAsync_ActiveUsers_CountsDistinctUsersNotEvents()
    {
        using var db = FreshDb();
        var user = await SeedUser(db, UserTier.Tier1);
        SeedEvent(db, user.Id, AnalyticsEventType.CvGenerated, Now.AddDays(-1));
        SeedEvent(db, user.Id, AnalyticsEventType.LetterGenerated, Now.AddDays(-2));
        SeedEvent(db, user.Id, AnalyticsEventType.AnswerGenerated, Now.AddDays(-3));
        await db.SaveChangesAsync();

        var summary = await AnalyticsService.GetSummaryAsync(db, Now);

        Assert.Equal(1, summary.ActiveUsersLast7Days);
    }

    // TC06 — Events older than 7 days don't count toward the active-user window.
    [Fact]
    public async Task GetSummaryAsync_ActiveUsers_ExcludesEventsOlderThan7Days()
    {
        using var db = FreshDb();
        var user = await SeedUser(db, UserTier.Tier1);
        SeedEvent(db, user.Id, AnalyticsEventType.Login, Now.AddDays(-8));
        await db.SaveChangesAsync();

        var summary = await AnalyticsService.GetSummaryAsync(db, Now);

        Assert.Equal(0, summary.ActiveUsersLast7Days);
    }
}
