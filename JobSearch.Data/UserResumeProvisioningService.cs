using Microsoft.EntityFrameworkCore;

namespace JobSearch.Data;

// Deterministic counterpart to ResumeBackfillAgent's LLM-driven reconciliation. That agent's
// entire job is reconciling a real free-text cv_base.md-style document against the structured
// Background to find where they diverge — for the onboarding "build from scratch" path (see
// ResumeIntakePage.tsx), there's no CvBase document to reconcile, so calling it would be both
// wasted LLM spend and untested-shape territory for an agent built to parse real documents.
// Same PK-reuse / "row absence means not yet provisioned" pattern as UserProfileProvisioningService.
public static class UserResumeProvisioningService
{
    public static async Task SeedDefaultIfMissingAsync(AppDbContext db, int userId)
    {
        if (await db.UserResumes.AnyAsync(r => r.UserId == userId)) return;

        // All-defaults row: ResumeRenderer.ParseOrDefault already treats "[]"/empty as "use
        // DefaultSectionConfig", so this is the correct default shape, not a stopgap — matches
        // what a real backfill would produce for a user with nothing yet to override.
        db.UserResumes.Add(new UserResume
        {
            UserId = userId,
            Summary = "",
            SectionConfigJson = "[]",
            ExperienceOverridesJson = "[]",
            SkillsSectionJson = "[]",
            ProjectOverridesJson = "[]",
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }
}
