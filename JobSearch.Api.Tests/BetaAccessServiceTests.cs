using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Api.Tests;

public class BetaAccessServiceTests
{
    private static AppDbContext FreshDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // TC01 — An email on the allowlist is allowed even with no existing account.
    [Fact]
    public async Task IsSignupAllowedAsync_EmailOnAllowlist_ReturnsTrue()
    {
        using var db = FreshDb();
        var allowlist = new HashSet<string> { "allowed@example.com" };

        var result = await BetaAccessService.IsSignupAllowedAsync(db, "allowed@example.com", allowlist);

        Assert.True(result);
    }

    // TC02 — An email not on the allowlist and with no existing account is rejected.
    [Fact]
    public async Task IsSignupAllowedAsync_EmailNotOnAllowlistNoAccount_ReturnsFalse()
    {
        using var db = FreshDb();
        var allowlist = new HashSet<string> { "allowed@example.com" };

        var result = await BetaAccessService.IsSignupAllowedAsync(db, "stranger@example.com", allowlist);

        Assert.False(result);
    }

    // TC03 — An existing user is always allowed back in, even after being removed from the
    // allowlist. Silent failure: without this, editing the allowlist to add a new beta tester
    // would accidentally lock out every previously-approved user on their next login.
    [Fact]
    public async Task IsSignupAllowedAsync_ExistingUserNotOnAllowlist_ReturnsTrue()
    {
        using var db = FreshDb();
        db.Users.Add(new User { Email = "veteran@example.com", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var allowlist = new HashSet<string> { "someone-else@example.com" };

        var result = await BetaAccessService.IsSignupAllowedAsync(db, "veteran@example.com", allowlist);

        Assert.True(result);
    }

    // TC04 — Comparison is case-insensitive and ignores surrounding whitespace, matching how
    // Google emails are normalized elsewhere (UserProvisioningService).
    [Fact]
    public async Task IsSignupAllowedAsync_DifferentCaseAndWhitespace_StillMatches()
    {
        using var db = FreshDb();
        var allowlist = new HashSet<string> { "allowed@example.com" };

        var result = await BetaAccessService.IsSignupAllowedAsync(db, "  Allowed@Example.com  ", allowlist);

        Assert.True(result);
    }
}
