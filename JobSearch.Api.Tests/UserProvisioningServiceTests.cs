using JobSearch.Api.Services;
using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Api.Tests;

public class UserProvisioningServiceTests
{
    private static AppDbContext FreshDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // TC01 — First login for an unseen email creates a row with the given defaults.
    [Fact]
    public async Task GetOrCreateAsync_UnseenEmail_CreatesUserWithGivenDefaults()
    {
        using var db = FreshDb();

        var user = await UserProvisioningService.GetOrCreateAsync(db, "new@example.com", UserTier.Tier2, 500);

        Assert.Equal("new@example.com", user.Email);
        Assert.Equal(UserTier.Tier2, user.Tier);
        Assert.Equal(500, user.CreditBalance);
        Assert.Single(db.Users);
    }

    // TC02 — Repeat login for the same email returns the existing row, not a new one.
    // Silent failure: without this, every login would insert a fresh row, breaking the
    // unique-email index and losing the user's tier/credit history on every sign-in.
    [Fact]
    public async Task GetOrCreateAsync_RepeatEmail_ReturnsExistingRowWithoutDuplicating()
    {
        using var db = FreshDb();
        var first = await UserProvisioningService.GetOrCreateAsync(db, "same@example.com");

        var second = await UserProvisioningService.GetOrCreateAsync(db, "same@example.com");

        Assert.Equal(first.Id, second.Id);
        Assert.Single(db.Users);
    }

    // TC03 — Emails that only differ by case resolve to the same account.
    // Silent failure: Google emails are case-insensitive, so "Foo@x.com" vs "foo@x.com"
    // would otherwise create two accounts for the same person and eventually collide on
    // the unique index the moment both variants are seen.
    [Fact]
    public async Task GetOrCreateAsync_SameEmailDifferentCase_ResolvesToSameUser()
    {
        using var db = FreshDb();
        var first = await UserProvisioningService.GetOrCreateAsync(db, "Person@Example.com");

        var second = await UserProvisioningService.GetOrCreateAsync(db, "person@example.com");

        Assert.Equal(first.Id, second.Id);
        Assert.Single(db.Users);
    }

    // TC04 — Login path (no explicit tier/credit args) defaults to Tier1 with zero credits.
    [Fact]
    public async Task GetOrCreateAsync_NoExplicitDefaults_UsesTier1AndZeroCredits()
    {
        using var db = FreshDb();

        var user = await UserProvisioningService.GetOrCreateAsync(db, "plain@example.com");

        Assert.Equal(UserTier.Tier1, user.Tier);
        Assert.Equal(0, user.CreditBalance);
    }

    // TC05 — Whitespace around the email doesn't cause a second row on the next lookup.
    [Fact]
    public async Task GetOrCreateAsync_EmailWithWhitespace_NormalizedBeforeLookup()
    {
        using var db = FreshDb();
        var first = await UserProvisioningService.GetOrCreateAsync(db, "  spaced@example.com  ");

        var second = await UserProvisioningService.GetOrCreateAsync(db, "spaced@example.com");

        Assert.Equal(first.Id, second.Id);
        Assert.Single(db.Users);
    }
}
