namespace JobSearch.Data;

// Isolates "make this user Tier 2" from how they got there. During the beta (no Stripe
// yet) a free self-serve endpoint calls this directly; post-launch, a Stripe
// payment-success webhook will call the exact same thing instead. Nothing payment-related
// belongs in here, so that swap doesn't touch this method or anything downstream of it
// (the sources/Gmail-connect funnel already keys off User.Tier alone).
public static class TierUpgradeService
{
    public static async Task<bool> UpgradeToTier2Async(AppDbContext db, int userId)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null || user.Tier == UserTier.Tier2) return false;

        user.Tier = UserTier.Tier2;
        await db.SaveChangesAsync();

        db.AnalyticsEvents.Add(new AnalyticsEvent { UserId = userId, EventType = AnalyticsEventType.Tier2Upgrade, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        return true;
    }
}
