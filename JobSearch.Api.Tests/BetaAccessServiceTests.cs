using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Api.Tests;

public class BetaAccessServiceTests
{
    private const string OwnerEmail = "owner@example.com";

    private static AppDbContext FreshDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // TC01 — The owner is always allowed in as Tier2, even with an empty invite table.
    // Silent failure: without this hardcoded path, a bug or empty BetaInvite table would
    // lock the owner out of their own app with no way back in.
    [Fact]
    public async Task ResolveSignupTierAsync_OwnerEmail_ReturnsTier2()
    {
        using var db = FreshDb();

        var tier = await BetaAccessService.ResolveSignupTierAsync(db, OwnerEmail, OwnerEmail);

        Assert.Equal(UserTier.Tier2, tier);
    }

    // TC02 — An invited email is allowed in as Tier2.
    [Fact]
    public async Task ResolveSignupTierAsync_InvitedEmail_ReturnsTier2()
    {
        using var db = FreshDb();
        db.BetaInvites.Add(new BetaInvite { Email = "invited@example.com", InvitedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var tier = await BetaAccessService.ResolveSignupTierAsync(db, "invited@example.com", OwnerEmail);

        Assert.Equal(UserTier.Tier2, tier);
    }

    // TC03 — An email that's neither the owner, invited, nor an existing user is rejected.
    [Fact]
    public async Task ResolveSignupTierAsync_UnknownEmail_ReturnsNull()
    {
        using var db = FreshDb();

        var tier = await BetaAccessService.ResolveSignupTierAsync(db, "stranger@example.com", OwnerEmail);

        Assert.Null(tier);
    }

    // TC04 — An existing user is always allowed back in, even if never invited (covers
    // users created before this gate existed, and anyone later removed from BetaInvite).
    [Fact]
    public async Task ResolveSignupTierAsync_ExistingUserNotInvited_ReturnsNonNull()
    {
        using var db = FreshDb();
        db.Users.Add(new User { Email = "veteran@example.com", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var tier = await BetaAccessService.ResolveSignupTierAsync(db, "veteran@example.com", OwnerEmail);

        Assert.NotNull(tier);
    }

    // TC05 — Comparison is case-insensitive and ignores surrounding whitespace, matching how
    // Google emails are normalized elsewhere (UserProvisioningService).
    [Fact]
    public async Task ResolveSignupTierAsync_DifferentCaseAndWhitespace_StillMatches()
    {
        using var db = FreshDb();
        db.BetaInvites.Add(new BetaInvite { Email = "invited@example.com", InvitedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var tier = await BetaAccessService.ResolveSignupTierAsync(db, "  Invited@Example.com  ", OwnerEmail);

        Assert.Equal(UserTier.Tier2, tier);
    }
}
