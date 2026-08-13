using Microsoft.EntityFrameworkCore;

namespace JobSearch.Data;

// Shared by JobSearch.Api's OnCreatingTicket/startup-seed and JobSearchAgent's worker
// startup, so every entry point that needs "the User row for this email" uses one
// get-or-create instead of copies drifting apart.
public static class UserProvisioningService
{
    public static async Task<User> GetOrCreateAsync(
        AppDbContext db, string email, string defaultTier = UserTier.Tier1, int defaultCreditBalance = 0)
    {
        // Google emails are effectively case-insensitive — normalize so "Foo@gmail.com" and
        // "foo@gmail.com" resolve to the same row instead of colliding on the unique index.
        email = email.Trim().ToLowerInvariant();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is not null) return user;

        user = new User { Email = email, Tier = defaultTier, CreditBalance = defaultCreditBalance, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
