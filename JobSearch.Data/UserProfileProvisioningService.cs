using Microsoft.EntityFrameworkCore;

namespace JobSearch.Data;

// Bridges the pre-multi-tenant world (one shared context/*.{yaml,md} file set) into the
// per-user UserProfile table: seeds a user's profile from given text exactly once, then
// leaves it alone so later edits (e.g. a future profile-settings UI) aren't clobbered on
// every restart.
public static class UserProfileProvisioningService
{
    public static async Task<UserProfile> GetOrSeedAsync(
        AppDbContext db, int userId, string background, string cvBase, string jobCriteria)
    {
        var profile = await db.UserProfiles.FindAsync(userId);
        if (profile is not null) return profile;

        profile = new UserProfile
        {
            UserId = userId,
            Background = background,
            CvBase = cvBase,
            JobCriteria = jobCriteria,
            UpdatedAt = DateTime.UtcNow,
        };
        db.UserProfiles.Add(profile);
        await db.SaveChangesAsync();
        return profile;
    }
}
