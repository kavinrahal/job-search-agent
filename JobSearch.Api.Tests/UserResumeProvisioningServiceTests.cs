using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Api.Tests;

public class UserResumeProvisioningServiceTests
{
    private static AppDbContext FreshDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // TC01 — From-scratch onboarding save (no UserResume row yet): seeds a default row with an
    // empty Summary and "[]" for every overrides column — ResumeRenderer.ParseOrDefault treats
    // "[]" as "use DefaultSectionConfig", so this is the deterministic default shape, not a
    // stopgap. No ResumeBackfillAgent/LLM call is involved — this method never takes one.
    [Fact]
    public async Task SeedDefaultIfMissingAsync_NoExistingRow_CreatesDefaultRow()
    {
        using var db = FreshDb();

        await UserResumeProvisioningService.SeedDefaultIfMissingAsync(db, userId: 1);

        var resume = await db.UserResumes.FindAsync(1);
        Assert.NotNull(resume);
        Assert.Equal("", resume!.Summary);
        Assert.Equal("[]", resume.SectionConfigJson);
        Assert.Equal("[]", resume.ExperienceOverridesJson);
        Assert.Equal("[]", resume.SkillsSectionJson);
        Assert.Equal("[]", resume.ProjectOverridesJson);
        Assert.Single(db.UserResumes);
    }

    // TC02 — A user who already has a UserResume row (e.g. already migrated via the
    // CvBase/ResumeBackfillAgent path, or already ran this seeding once) is left untouched —
    // this must never clobber real curated content with the empty default.
    [Fact]
    public async Task SeedDefaultIfMissingAsync_RowAlreadyExists_DoesNotOverwrite()
    {
        using var db = FreshDb();
        db.UserResumes.Add(new UserResume
        {
            UserId = 1,
            Summary = "Real curated summary",
            SectionConfigJson = "[{\"sectionKey\":\"experience\",\"included\":true}]",
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await UserResumeProvisioningService.SeedDefaultIfMissingAsync(db, userId: 1);

        var resume = await db.UserResumes.FindAsync(1);
        Assert.Equal("Real curated summary", resume!.Summary);
        Assert.Single(db.UserResumes);
    }
}
