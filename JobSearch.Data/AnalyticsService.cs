using Microsoft.EntityFrameworkCore;

namespace JobSearch.Data;

public record TierCount(string Tier, int Count);
public record EventCount(string EventType, int Count);
public record AnalyticsSummary(int TotalUsers, IReadOnlyList<TierCount> TierBreakdown, int ActiveUsersLast7Days, IReadOnlyList<EventCount> EventCountsLast30Days);

public static class AnalyticsService
{
    public static async Task<AnalyticsSummary> GetSummaryAsync(AppDbContext db, DateTime now)
    {
        var since30d = now.AddDays(-30);
        var since7d = now.AddDays(-7);

        var tierBreakdown = await db.Users
            .GroupBy(u => u.Tier)
            .Select(g => new TierCount(g.Key, g.Count()))
            .ToListAsync();

        var eventCountsLast30Days = await db.AnalyticsEvents
            .Where(e => e.CreatedAt >= since30d)
            .GroupBy(e => e.EventType)
            .Select(g => new EventCount(g.Key, g.Count()))
            .ToListAsync();

        var activeUsersLast7Days = await db.AnalyticsEvents
            .Where(e => e.CreatedAt >= since7d)
            .Select(e => e.UserId)
            .Distinct()
            .CountAsync();

        return new AnalyticsSummary(await db.Users.CountAsync(), tierBreakdown, activeUsersLast7Days, eventCountsLast30Days);
    }
}
