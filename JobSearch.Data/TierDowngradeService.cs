namespace JobSearch.Data;

// The self-serve counterpart to TierUpgradeService: mirrors its shape exactly (find-user,
// no-op-if-already-there, flip tier, single AnalyticsEvent) so the two stay easy to compare.
// The one extra step downgrade needs that upgrade doesn't: forfeiting unused credits. A Tier1
// account was never meant to hold a Tier2-sized balance, so leaving one in place after a
// downgrade would just be a stale number nothing else in the app expects — CreditBalance is
// reset to 0, the same baseline UserProvisioningService.GetOrCreateAsync gives every brand-new
// Tier1 signup (see its defaultCreditBalance default), not some other "kept" fraction.
public static class TierDowngradeService
{
    public static async Task<bool> DowngradeToTier1Async(AppDbContext db, int userId)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null || user.Tier == UserTier.Tier1) return false;

        user.Tier = UserTier.Tier1;
        // Same CreditVersion bump convention as every other write to CreditBalance
        // (CreditService, AdjustCredit) — keeps the optimistic-concurrency token consistent
        // even though this write itself doesn't race against a concurrent spend.
        user.CreditBalance = 0;
        user.CreditVersion += 1;
        await db.SaveChangesAsync();

        db.AnalyticsEvents.Add(new AnalyticsEvent { UserId = userId, EventType = AnalyticsEventType.Tier2Downgrade, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        return true;
    }
}
