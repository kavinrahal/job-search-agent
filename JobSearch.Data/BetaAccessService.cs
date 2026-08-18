using Microsoft.EntityFrameworkCore;

namespace JobSearch.Data;

// Gates new signups during the beta to an explicit email allowlist, without locking out
// anyone who's already signed up if the list changes later — only brand-new accounts are
// checked against it. Existing users keep working even if removed from a future allowlist
// (removal is a separate, deliberate account-deactivation action, not implicit).
public static class BetaAccessService
{
    public static async Task<bool> IsSignupAllowedAsync(AppDbContext db, string email, IReadOnlySet<string> allowlist)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (allowlist.Contains(normalized)) return true;
        return await db.Users.AnyAsync(u => u.Email == normalized);
    }
}
