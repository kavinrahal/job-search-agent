using Microsoft.EntityFrameworkCore;

namespace JobSearch.Data;

// Gates new signups during the beta. Three ways in: the hardcoded owner email (must always
// work regardless of table state — the bootstrapping account that can fix everything else),
// an existing account (never re-gated once created), or a row in BetaInvite (the owner
// personally choosing who gets Tier 2 access, via the admin invite endpoint).
public static class BetaAccessService
{
    // Null return means signup isn't allowed at all. A non-null return is the tier a
    // brand-new account should be created with — irrelevant for an existing user, since
    // UserProvisioningService.GetOrCreateAsync ignores the tier argument for them.
    public static async Task<string?> ResolveSignupTierAsync(AppDbContext db, string email, string ownerEmail)
    {
        var normalized = email.Trim().ToLowerInvariant();

        if (normalized == ownerEmail.Trim().ToLowerInvariant()) return UserTier.Tier2;
        if (await db.Users.AnyAsync(u => u.Email == normalized)) return UserTier.Tier1;
        if (await db.BetaInvites.AnyAsync(i => i.Email == normalized)) return UserTier.Tier2;
        return null;
    }
}
