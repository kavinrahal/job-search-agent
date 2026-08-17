using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Api.Tests;

public class TierUpgradeServiceTests
{
    private static AppDbContext FreshDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<int> SeedUserAsync(AppDbContext db, string tier)
    {
        var user = new User { Email = $"{Guid.NewGuid()}@example.com", Tier = tier, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    // TC01 — A Tier1 user is flipped to Tier2 and the call reports success.
    [Fact]
    public async Task UpgradeToTier2Async_Tier1User_FlipsTierAndReturnsTrue()
    {
        using var db = FreshDb();
        var userId = await SeedUserAsync(db, UserTier.Tier1);

        var result = await TierUpgradeService.UpgradeToTier2Async(db, userId);

        Assert.True(result);
        Assert.Equal(UserTier.Tier2, (await db.Users.FindAsync(userId))!.Tier);
    }

    // TC02 — An already-Tier2 user is left untouched and the call reports no-op.
    // Silent failure: without this, a double-click or retry would still report "success"
    // while doing nothing, or worse, re-fire the analytics event as if it were a new upgrade.
    [Fact]
    public async Task UpgradeToTier2Async_AlreadyTier2_ReturnsFalseAndDoesNotDuplicateEvent()
    {
        using var db = FreshDb();
        var userId = await SeedUserAsync(db, UserTier.Tier2);

        var result = await TierUpgradeService.UpgradeToTier2Async(db, userId);

        Assert.False(result);
        Assert.Empty(db.AnalyticsEvents.Where(e => e.UserId == userId));
    }

    // TC03 — An unknown user id returns false rather than throwing.
    [Fact]
    public async Task UpgradeToTier2Async_UnknownUser_ReturnsFalse()
    {
        using var db = FreshDb();

        var result = await TierUpgradeService.UpgradeToTier2Async(db, userId: 999);

        Assert.False(result);
    }

    // TC04 — A successful upgrade records exactly one Tier2Upgrade analytics event.
    [Fact]
    public async Task UpgradeToTier2Async_Tier1User_RecordsOneUpgradeEvent()
    {
        using var db = FreshDb();
        var userId = await SeedUserAsync(db, UserTier.Tier1);

        await TierUpgradeService.UpgradeToTier2Async(db, userId);

        var events = db.AnalyticsEvents.Where(e => e.UserId == userId).ToList();
        var upgradeEvent = Assert.Single(events);
        Assert.Equal(AnalyticsEventType.Tier2Upgrade, upgradeEvent.EventType);
    }
}
