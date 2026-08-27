using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Api.Tests;

public class UserVerificationTokenServiceTests
{
    private static AppDbContext FreshDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<int> SeedUserAsync(AppDbContext db)
    {
        var user = new User { Email = $"{Guid.NewGuid()}@example.com", CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    // TC01 — A freshly issued, unexpired token validates and resolves the right user.
    [Fact]
    public async Task ValidateAndConsumeAsync_FreshToken_ResolvesUser()
    {
        using var db = FreshDb();
        var userId = await SeedUserAsync(db);
        var token = await UserVerificationTokenService.IssueAsync(db, userId, UserVerificationTokenPurpose.EmailVerification);

        var user = await UserVerificationTokenService.ValidateAndConsumeAsync(db, token, UserVerificationTokenPurpose.EmailVerification);

        Assert.NotNull(user);
        Assert.Equal(userId, user!.Id);
    }

    // TC02 — Only a hash of the token is ever persisted — the raw token can't be recovered
    // from a DB read, unlike InboundEmailToken's long-lived raw-storage choice.
    [Fact]
    public async Task IssueAsync_PersistsHashNotRawToken()
    {
        using var db = FreshDb();
        var userId = await SeedUserAsync(db);

        var token = await UserVerificationTokenService.IssueAsync(db, userId, UserVerificationTokenPurpose.EmailVerification);

        var record = await db.UserVerificationTokens.SingleAsync();
        Assert.NotEqual(token, record.TokenHash);
    }

    // TC03 — Using a token a second time fails — replay is prevented.
    // Silent failure: without consuming on use, a leaked (e.g. logged, forwarded) verification
    // or reset link could be replayed indefinitely within its expiry window.
    [Fact]
    public async Task ValidateAndConsumeAsync_AlreadyConsumedToken_ReturnsNull()
    {
        using var db = FreshDb();
        var userId = await SeedUserAsync(db);
        var token = await UserVerificationTokenService.IssueAsync(db, userId, UserVerificationTokenPurpose.PasswordReset);
        var first = await UserVerificationTokenService.ValidateAndConsumeAsync(db, token, UserVerificationTokenPurpose.PasswordReset);
        Assert.NotNull(first);

        var second = await UserVerificationTokenService.ValidateAndConsumeAsync(db, token, UserVerificationTokenPurpose.PasswordReset);

        Assert.Null(second);
    }

    // TC04 — An expired token is rejected even though it was never consumed.
    [Fact]
    public async Task ValidateAndConsumeAsync_ExpiredToken_ReturnsNull()
    {
        using var db = FreshDb();
        var userId = await SeedUserAsync(db);
        var token = UserVerificationTokenService.GenerateToken();
        // Issue directly with a past expiry — IssueAsync always sets a fresh ~1hr window, so
        // this simulates the passage of time without needing to actually wait.
        db.UserVerificationTokens.Add(new UserVerificationToken
        {
            UserId = userId,
            TokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token))).ToLowerInvariant(),
            Purpose = UserVerificationTokenPurpose.EmailVerification,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddHours(-1),
        });
        await db.SaveChangesAsync();

        var result = await UserVerificationTokenService.ValidateAndConsumeAsync(db, token, UserVerificationTokenPurpose.EmailVerification);

        Assert.Null(result);
    }

    // TC05 — A token issued for one purpose doesn't validate against the other purpose, even
    // with a correct, unexpired, unconsumed hash match.
    [Fact]
    public async Task ValidateAndConsumeAsync_WrongPurpose_ReturnsNull()
    {
        using var db = FreshDb();
        var userId = await SeedUserAsync(db);
        var token = await UserVerificationTokenService.IssueAsync(db, userId, UserVerificationTokenPurpose.EmailVerification);

        var result = await UserVerificationTokenService.ValidateAndConsumeAsync(db, token, UserVerificationTokenPurpose.PasswordReset);

        Assert.Null(result);
    }

    // TC06 — An unknown token (never issued) is rejected rather than throwing.
    [Fact]
    public async Task ValidateAndConsumeAsync_UnknownToken_ReturnsNull()
    {
        using var db = FreshDb();

        var result = await UserVerificationTokenService.ValidateAndConsumeAsync(db, "not-a-real-token", UserVerificationTokenPurpose.EmailVerification);

        Assert.Null(result);
    }

    // TC07 — Many generated tokens never collide (mirrors InboundEmailService's own guarantee).
    [Fact]
    public void GenerateToken_CalledManyTimes_NeverCollides()
    {
        var tokens = Enumerable.Range(0, 1000).Select(_ => UserVerificationTokenService.GenerateToken()).ToHashSet();
        Assert.Equal(1000, tokens.Count);
    }
}
