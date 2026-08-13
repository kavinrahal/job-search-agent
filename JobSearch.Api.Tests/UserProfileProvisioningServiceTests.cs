using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Api.Tests;

public class UserProfileProvisioningServiceTests
{
    private static AppDbContext FreshDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // TC01 — First seed for a user with no profile yet creates one with the given content.
    [Fact]
    public async Task GetOrSeedAsync_NoExistingProfile_CreatesWithGivenContent()
    {
        using var db = FreshDb();

        var profile = await UserProfileProvisioningService.GetOrSeedAsync(
            db, userId: 1, background: "bg", cvBase: "cv", jobCriteria: "criteria");

        Assert.Equal(1, profile.UserId);
        Assert.Equal("bg", profile.Background);
        Assert.Equal("cv", profile.CvBase);
        Assert.Equal("criteria", profile.JobCriteria);
        Assert.Single(db.UserProfiles);
    }

    // TC02 — A user who already has a profile is left untouched by a later seed call.
    // Silent failure: without this, every app restart would silently overwrite whatever a
    // user (or a future profile-editing UI) had saved with the on-disk file content again.
    [Fact]
    public async Task GetOrSeedAsync_ProfileAlreadyExists_DoesNotOverwriteExistingContent()
    {
        using var db = FreshDb();
        await UserProfileProvisioningService.GetOrSeedAsync(
            db, userId: 1, background: "original", cvBase: "cv", jobCriteria: "criteria");

        var result = await UserProfileProvisioningService.GetOrSeedAsync(
            db, userId: 1, background: "would-overwrite", cvBase: "cv2", jobCriteria: "criteria2");

        Assert.Equal("original", result.Background);
        Assert.Single(db.UserProfiles);
    }
}
