using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Api.Tests;

public class TierDowngradeServiceTests
{
    private static AppDbContext FreshDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<int> SeedUserAsync(AppDbContext db, string tier, int creditBalance = 0)
    {
        var user = new User { Email = $"{Guid.NewGuid()}@example.com", Tier = tier, CreditBalance = creditBalance, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    // TC01 — A Tier2 user is flipped to Tier1 and the call reports success.
    [Fact]
    public async Task DowngradeToTier1Async_Tier2User_FlipsTierAndReturnsTrue()
    {
        using var db = FreshDb();
        var userId = await SeedUserAsync(db, UserTier.Tier2);

        var result = await TierDowngradeService.DowngradeToTier1Async(db, userId);

        Assert.True(result);
        Assert.Equal(UserTier.Tier1, (await db.Users.FindAsync(userId))!.Tier);
    }

    // TC02 — An already-Tier1 user is left untouched and the call reports no-op. Same
    // reasoning as TierUpgradeServiceTests' equivalent: a double-click/retry must not report
    // "success" while doing nothing, or re-fire the downgrade analytics event.
    [Fact]
    public async Task DowngradeToTier1Async_AlreadyTier1_ReturnsFalseAndDoesNotDuplicateEvent()
    {
        using var db = FreshDb();
        var userId = await SeedUserAsync(db, UserTier.Tier1, creditBalance: 5);

        var result = await TierDowngradeService.DowngradeToTier1Async(db, userId);

        Assert.False(result);
        Assert.Equal(5, (await db.Users.FindAsync(userId))!.CreditBalance);
        Assert.Empty(db.AnalyticsEvents.Where(e => e.UserId == userId));
    }

    // TC03 — An unknown user id returns false rather than throwing.
    [Fact]
    public async Task DowngradeToTier1Async_UnknownUser_ReturnsFalse()
    {
        using var db = FreshDb();

        var result = await TierDowngradeService.DowngradeToTier1Async(db, userId: 999);

        Assert.False(result);
    }

    // TC04 — A successful downgrade records exactly one Tier2Downgrade analytics event.
    [Fact]
    public async Task DowngradeToTier1Async_Tier2User_RecordsOneDowngradeEvent()
    {
        using var db = FreshDb();
        var userId = await SeedUserAsync(db, UserTier.Tier2);

        await TierDowngradeService.DowngradeToTier1Async(db, userId);

        var events = db.AnalyticsEvents.Where(e => e.UserId == userId).ToList();
        var downgradeEvent = Assert.Single(events);
        Assert.Equal(AnalyticsEventType.Tier2Downgrade, downgradeEvent.EventType);
    }

    // TC05 — The core behavior this endpoint exists for: any unused credit balance is
    // forfeited immediately, reset to the same 0 baseline a brand-new Tier1 signup gets
    // (UserProvisioningService.GetOrCreateAsync's defaultCreditBalance), not left as a
    // stale Tier2-sized number on a Tier1 account.
    [Fact]
    public async Task DowngradeToTier1Async_Tier2UserWithCredits_ForfeitsCreditsToZero()
    {
        using var db = FreshDb();
        var userId = await SeedUserAsync(db, UserTier.Tier2, creditBalance: 42);

        await TierDowngradeService.DowngradeToTier1Async(db, userId);

        Assert.Equal(0, (await db.Users.FindAsync(userId))!.CreditBalance);
    }
}
