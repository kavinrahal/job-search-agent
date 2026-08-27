using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Api.Tests;

public class PasswordAuthServiceTests
{
    private const string OwnerEmail = "owner@example.com";
    private const string ValidPassword = "Abcdef1!";

    private static AppDbContext FreshDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task InviteAsync(AppDbContext db, string email)
    {
        db.BetaInvites.Add(new BetaInvite { Email = email, InvitedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    // TC01 — Full happy path: an invited email registers, verifies via the emailed token, and
    // then logs in successfully with the password it registered.
    [Fact]
    public async Task RegisterThenVerifyThenLogin_FullRoundTrip_Succeeds()
    {
        using var db = FreshDb();
        const string email = "newuser@example.com";
        await InviteAsync(db, email);

        var register = await PasswordAuthService.RegisterAsync(db, email, ValidPassword, OwnerEmail);
        Assert.Equal(PasswordAuthService.RegisterOutcome.VerificationSent, register.Outcome);
        Assert.NotNull(register.VerificationToken);

        // Not yet usable — the account exists but isn't verified.
        var loginBeforeVerify = await PasswordAuthService.LoginAsync(db, email, ValidPassword);
        Assert.Equal(PasswordAuthService.LoginOutcome.NotVerified, loginBeforeVerify.Outcome);

        var verifiedUser = await PasswordAuthService.VerifyEmailAsync(db, register.VerificationToken!);
        Assert.NotNull(verifiedUser);
        Assert.NotNull(verifiedUser!.EmailVerifiedAt);

        var loginAfterVerify = await PasswordAuthService.LoginAsync(db, email, ValidPassword);
        Assert.Equal(PasswordAuthService.LoginOutcome.Success, loginAfterVerify.Outcome);
        Assert.Equal(verifiedUser.Id, loginAfterVerify.User!.Id);
    }

    // TC02 — A password failing the rules is rejected before anything else runs — no invite
    // check, no row created — even for an email that was never invited.
    [Fact]
    public async Task RegisterAsync_InvalidPassword_RejectsBeforeCreatingAnyRow()
    {
        using var db = FreshDb();

        var result = await PasswordAuthService.RegisterAsync(db, "nobody@example.com", "weak", OwnerEmail);

        Assert.Equal(PasswordAuthService.RegisterOutcome.PasswordInvalid, result.Outcome);
        Assert.NotEmpty(result.PasswordErrors);
        Assert.Empty(db.Users);
    }

    // TC03 — Each individual missing character class is rejected on its own.
    [Theory]
    [InlineData("short1!")]   // too short
    [InlineData("abcdefg1!")] // no uppercase
    [InlineData("ABCDEFG1!")] // no lowercase
    [InlineData("Abcdefgh!")] // no digit
    [InlineData("Abcdefgh1")] // no special character
    public async Task RegisterAsync_EachInvalidPasswordVariant_ReturnsPasswordInvalid(string badPassword)
    {
        using var db = FreshDb();
        const string email = "variant@example.com";
        await InviteAsync(db, email);

        var result = await PasswordAuthService.RegisterAsync(db, email, badPassword, OwnerEmail);

        Assert.Equal(PasswordAuthService.RegisterOutcome.PasswordInvalid, result.Outcome);
    }

    // TC04 — A genuinely uninvited email is rejected, same invite gate Google login uses.
    [Fact]
    public async Task RegisterAsync_NotInvited_ReturnsNotInvited()
    {
        using var db = FreshDb();

        var result = await PasswordAuthService.RegisterAsync(db, "stranger@example.com", ValidPassword, OwnerEmail);

        Assert.Equal(PasswordAuthService.RegisterOutcome.NotInvited, result.Outcome);
        Assert.Empty(db.Users);
    }

    // TC05 — The account-linking safety net: registering with an email that already has a
    // Google-created row (EmailVerifiedAt set, no PasswordHash) attaches the password to that
    // *same* row — no duplicate — and signs in immediately without a verification email,
    // since Google already proved ownership of this email.
    [Fact]
    public async Task RegisterAsync_EmailMatchesExistingGoogleAccount_LinksToSameUserAndSignsInImmediately()
    {
        using var db = FreshDb();
        const string email = "googleuser@example.com";
        var googleUser = new User
        {
            Email = email,
            Tier = UserTier.Tier1,
            CreatedAt = DateTime.UtcNow,
            EmailVerifiedAt = DateTime.UtcNow.AddDays(-3), // proved via a prior Google login
        };
        db.Users.Add(googleUser);
        await db.SaveChangesAsync();

        var result = await PasswordAuthService.RegisterAsync(db, email, ValidPassword, OwnerEmail);

        Assert.Equal(PasswordAuthService.RegisterOutcome.SignedIn, result.Outcome);
        Assert.Equal(googleUser.Id, result.User!.Id);
        Assert.Single(db.Users); // still exactly one row — no duplicate created
        Assert.NotNull((await db.Users.FindAsync(googleUser.Id))!.PasswordHash);
    }

    // TC06 — Registering twice for the same email (password already set) is rejected on the
    // second attempt instead of overwriting the existing password.
    [Fact]
    public async Task RegisterAsync_EmailAlreadyHasPassword_ReturnsAlreadyHasPassword()
    {
        using var db = FreshDb();
        const string email = "repeat@example.com";
        await InviteAsync(db, email);
        var first = await PasswordAuthService.RegisterAsync(db, email, ValidPassword, OwnerEmail);
        Assert.Equal(PasswordAuthService.RegisterOutcome.VerificationSent, first.Outcome);

        var second = await PasswordAuthService.RegisterAsync(db, email, "AnotherPass9!", OwnerEmail);

        Assert.Equal(PasswordAuthService.RegisterOutcome.AlreadyHasPassword, second.Outcome);
    }

    // TC07 — Wrong password against a real, verified account is rejected.
    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsInvalidCredentials()
    {
        using var db = FreshDb();
        const string email = "verified@example.com";
        await InviteAsync(db, email);
        var register = await PasswordAuthService.RegisterAsync(db, email, ValidPassword, OwnerEmail);
        await PasswordAuthService.VerifyEmailAsync(db, register.VerificationToken!);

        var result = await PasswordAuthService.LoginAsync(db, email, "TotallyWrong1!");

        Assert.Equal(PasswordAuthService.LoginOutcome.InvalidCredentials, result.Outcome);
        Assert.Null(result.User);
    }

    // TC08 — Anti-enumeration guarantee: a login attempt for an email that doesn't exist at
    // all produces the *exact same* outcome and payload as a login attempt for a real,
    // password-holding account with the wrong password — not just "both fail", genuinely
    // indistinguishable to the caller. This is what Program.cs's /auth/login endpoint maps
    // 1:1 into the HTTP response body, so identical LoginResult here means identical JSON.
    [Fact]
    public async Task LoginAsync_UnknownEmailVsWrongPasswordForRealAccount_ProduceIdenticalResult()
    {
        using var db = FreshDb();
        const string realEmail = "real@example.com";
        await InviteAsync(db, realEmail);
        var register = await PasswordAuthService.RegisterAsync(db, realEmail, ValidPassword, OwnerEmail);
        await PasswordAuthService.VerifyEmailAsync(db, register.VerificationToken!);

        var unknownEmailResult = await PasswordAuthService.LoginAsync(db, "nobodyhere@example.com", "SomePass1!");
        var wrongPasswordResult = await PasswordAuthService.LoginAsync(db, realEmail, "WrongPass1!");

        Assert.Equal(unknownEmailResult.Outcome, wrongPasswordResult.Outcome);
        Assert.Equal(PasswordAuthService.LoginOutcome.InvalidCredentials, unknownEmailResult.Outcome);
        Assert.Null(unknownEmailResult.User);
        Assert.Null(wrongPasswordResult.User);
    }

    // TC09 — A Google-only account (no password ever set) also collapses to the exact same
    // InvalidCredentials outcome as an unknown email — a login attempt can't be used to probe
    // "does this email have a Google account" either.
    [Fact]
    public async Task LoginAsync_GoogleOnlyAccountNoPassword_ReturnsSameOutcomeAsUnknownEmail()
    {
        using var db = FreshDb();
        db.Users.Add(new User { Email = "googleonly@example.com", CreatedAt = DateTime.UtcNow, EmailVerifiedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var googleOnlyResult = await PasswordAuthService.LoginAsync(db, "googleonly@example.com", "AnyPass1!");
        var unknownResult = await PasswordAuthService.LoginAsync(db, "nobody@example.com", "AnyPass1!");

        Assert.Equal(unknownResult.Outcome, googleOnlyResult.Outcome);
        Assert.Equal(PasswordAuthService.LoginOutcome.InvalidCredentials, googleOnlyResult.Outcome);
    }

    // TC10 — An unverified account is rejected at login with the distinct "not verified"
    // outcome (an accepted UX tradeoff, unlike the generic InvalidCredentials above) until the
    // verification link is used.
    [Fact]
    public async Task LoginAsync_UnverifiedAccount_ReturnsNotVerifiedUntilVerified()
    {
        using var db = FreshDb();
        const string email = "pending@example.com";
        await InviteAsync(db, email);
        var register = await PasswordAuthService.RegisterAsync(db, email, ValidPassword, OwnerEmail);

        var beforeVerify = await PasswordAuthService.LoginAsync(db, email, ValidPassword);
        Assert.Equal(PasswordAuthService.LoginOutcome.NotVerified, beforeVerify.Outcome);

        await PasswordAuthService.VerifyEmailAsync(db, register.VerificationToken!);
        var afterVerify = await PasswordAuthService.LoginAsync(db, email, ValidPassword);

        Assert.Equal(PasswordAuthService.LoginOutcome.Success, afterVerify.Outcome);
    }

    // TC11 — A verification token can't be reused once consumed.
    [Fact]
    public async Task VerifyEmailAsync_AlreadyConsumedToken_ReturnsNull()
    {
        using var db = FreshDb();
        const string email = "onceonly@example.com";
        await InviteAsync(db, email);
        var register = await PasswordAuthService.RegisterAsync(db, email, ValidPassword, OwnerEmail);
        var first = await PasswordAuthService.VerifyEmailAsync(db, register.VerificationToken!);
        Assert.NotNull(first);

        var second = await PasswordAuthService.VerifyEmailAsync(db, register.VerificationToken!);

        Assert.Null(second);
    }

    // TC12 — An expired verification token is rejected.
    [Fact]
    public async Task VerifyEmailAsync_ExpiredToken_ReturnsNull()
    {
        using var db = FreshDb();
        const string email = "expired@example.com";
        await InviteAsync(db, email);
        var register = await PasswordAuthService.RegisterAsync(db, email, ValidPassword, OwnerEmail);
        var record = await db.UserVerificationTokens.SingleAsync();
        record.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var result = await PasswordAuthService.VerifyEmailAsync(db, register.VerificationToken!);

        Assert.Null(result);
    }

    // TC13 — forgot-password never reveals whether the email exists: an unknown email and a
    // Google-only account (no password to reset) both return no token/no user to send to,
    // exactly like a real password-holding account would if it also failed for some reason —
    // the HTTP layer returns the same generic message regardless of this result.
    [Fact]
    public async Task RequestPasswordResetAsync_UnknownEmail_ReturnsNoUserAndNoToken()
    {
        using var db = FreshDb();

        var (user, token) = await PasswordAuthService.RequestPasswordResetAsync(db, "ghost@example.com");

        Assert.Null(user);
        Assert.Null(token);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_GoogleOnlyAccount_ReturnsNoUserAndNoToken()
    {
        using var db = FreshDb();
        db.Users.Add(new User { Email = "googleonly2@example.com", CreatedAt = DateTime.UtcNow, EmailVerifiedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var (user, token) = await PasswordAuthService.RequestPasswordResetAsync(db, "googleonly2@example.com");

        Assert.Null(user);
        Assert.Null(token);
    }

    // TC14 — A real password-holding account gets a token, and resetting with it changes the
    // password: the old password stops working, the new one logs in.
    [Fact]
    public async Task RequestPasswordResetAsync_ThenResetPasswordAsync_ChangesPasswordSuccessfully()
    {
        using var db = FreshDb();
        const string email = "resetme@example.com";
        await InviteAsync(db, email);
        var register = await PasswordAuthService.RegisterAsync(db, email, ValidPassword, OwnerEmail);
        await PasswordAuthService.VerifyEmailAsync(db, register.VerificationToken!);

        var (user, token) = await PasswordAuthService.RequestPasswordResetAsync(db, email);
        Assert.NotNull(user);
        Assert.NotNull(token);

        const string newPassword = "BrandNew9!";
        var reset = await PasswordAuthService.ResetPasswordAsync(db, token!, newPassword);
        Assert.Equal(PasswordAuthService.ResetPasswordOutcome.Success, reset.Outcome);

        var loginOld = await PasswordAuthService.LoginAsync(db, email, ValidPassword);
        var loginNew = await PasswordAuthService.LoginAsync(db, email, newPassword);

        Assert.Equal(PasswordAuthService.LoginOutcome.InvalidCredentials, loginOld.Outcome);
        Assert.Equal(PasswordAuthService.LoginOutcome.Success, loginNew.Outcome);
    }

    // TC15 — reset-password validates the new password against the same rules as register.
    [Fact]
    public async Task ResetPasswordAsync_InvalidNewPassword_ReturnsPasswordInvalid()
    {
        using var db = FreshDb();
        const string email = "resetweak@example.com";
        await InviteAsync(db, email);
        var register = await PasswordAuthService.RegisterAsync(db, email, ValidPassword, OwnerEmail);
        await PasswordAuthService.VerifyEmailAsync(db, register.VerificationToken!);
        var (_, token) = await PasswordAuthService.RequestPasswordResetAsync(db, email);

        var result = await PasswordAuthService.ResetPasswordAsync(db, token!, "weak");

        Assert.Equal(PasswordAuthService.ResetPasswordOutcome.PasswordInvalid, result.Outcome);
    }

    // TC16 — A reset token can't be reused once consumed (same replay guarantee as verification).
    [Fact]
    public async Task ResetPasswordAsync_AlreadyConsumedToken_ReturnsInvalidToken()
    {
        using var db = FreshDb();
        const string email = "resetonce@example.com";
        await InviteAsync(db, email);
        var register = await PasswordAuthService.RegisterAsync(db, email, ValidPassword, OwnerEmail);
        await PasswordAuthService.VerifyEmailAsync(db, register.VerificationToken!);
        var (_, token) = await PasswordAuthService.RequestPasswordResetAsync(db, email);
        var first = await PasswordAuthService.ResetPasswordAsync(db, token!, "FirstNew1!");
        Assert.Equal(PasswordAuthService.ResetPasswordOutcome.Success, first.Outcome);

        var second = await PasswordAuthService.ResetPasswordAsync(db, token!, "SecondNew1!");

        Assert.Equal(PasswordAuthService.ResetPasswordOutcome.InvalidToken, second.Outcome);
    }

    // TC17 — An unknown/garbage reset token is rejected rather than throwing.
    [Fact]
    public async Task ResetPasswordAsync_UnknownToken_ReturnsInvalidToken()
    {
        using var db = FreshDb();

        var result = await PasswordAuthService.ResetPasswordAsync(db, "not-a-real-token", ValidPassword);

        Assert.Equal(PasswordAuthService.ResetPasswordOutcome.InvalidToken, result.Outcome);
    }
}
