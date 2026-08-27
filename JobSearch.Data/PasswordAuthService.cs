using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Data;

// Core email/password auth logic — DB and hashing only, no HttpContext/cookie/email-sending
// concerns, so it's testable the same way as BetaAccessService/UserProvisioningService (plain
// AppDbContext, no ASP.NET host needed). JobSearch.Api/Program.cs's /auth/* endpoints are thin
// wrappers: they call in here, then do the HTTP-specific bits (SignInAsync, sending the
// verification/reset email via SendGridEmailService, mapping outcomes to responses).
//
// This is a second, fully independent login path alongside Google OAuth — nothing here is
// called from or changes the Google flow, except that Google's own OnCreatingTicket (Program.cs)
// now also stamps User.EmailVerifiedAt on first login, which is what lets a later password
// registration against the same email skip re-verification (the account-linking safety net).
public static class PasswordAuthService
{
    public enum RegisterOutcome
    {
        PasswordInvalid,
        NotInvited,
        AlreadyHasPassword,
        VerificationSent,  // brand-new (or previously Google-only-but-unverified) account — email sent, do not sign in yet
        SignedIn,           // EmailVerifiedAt was already set (prior Google login already proved ownership) — sign in immediately
    }

    public class RegisterResult
    {
        public required RegisterOutcome Outcome { get; init; }
        public List<string> PasswordErrors { get; init; } = [];
        public User? User { get; init; }

        // Set only when Outcome == VerificationSent — the raw token for the caller to email.
        public string? VerificationToken { get; init; }
    }

    public static async Task<RegisterResult> RegisterAsync(
        AppDbContext db, string email, string password, string ownerEmail)
    {
        var passwordErrors = PasswordRules.Validate(password);
        if (passwordErrors.Count > 0)
            return new RegisterResult { Outcome = RegisterOutcome.PasswordInvalid, PasswordErrors = passwordErrors };

        // Same invite gate Google login uses — checked before any row is touched, same as
        // OnCreatingTicket's own ordering.
        var signupTier = await BetaAccessService.ResolveSignupTierAsync(db, email, ownerEmail);
        if (signupTier is null)
            return new RegisterResult { Outcome = RegisterOutcome.NotInvited };

        // The account-linking moment: an email that already has a Google-created row resolves
        // to that same row here instead of creating a duplicate.
        var user = await UserProvisioningService.GetOrCreateAsync(db, email, signupTier);

        if (user.PasswordHash is not null)
            return new RegisterResult { Outcome = RegisterOutcome.AlreadyHasPassword, User = user };

        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);

        // Google already proved this email's ownership on a prior login — no need to make
        // them click a verification link too.
        if (user.EmailVerifiedAt is not null)
        {
            await db.SaveChangesAsync();
            return new RegisterResult { Outcome = RegisterOutcome.SignedIn, User = user };
        }

        await db.SaveChangesAsync();
        var token = await UserVerificationTokenService.IssueAsync(db, user.Id, UserVerificationTokenPurpose.EmailVerification);
        return new RegisterResult { Outcome = RegisterOutcome.VerificationSent, User = user, VerificationToken = token };
    }

    // Consumes an email-verification token, marks the email verified, and signs the user in
    // (the caller does the actual SignInAsync). Null return means an invalid/expired/already-
    // used token.
    public static async Task<User?> VerifyEmailAsync(AppDbContext db, string token)
    {
        var user = await UserVerificationTokenService.ValidateAndConsumeAsync(db, token, UserVerificationTokenPurpose.EmailVerification);
        if (user is null) return null;

        user.EmailVerifiedAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync();
        return user;
    }

    public enum LoginOutcome
    {
        InvalidCredentials, // user not found, no password set, or wrong password — deliberately indistinguishable
        NotVerified,
        Success,
    }

    public class LoginResult
    {
        public required LoginOutcome Outcome { get; init; }
        public User? User { get; init; }
    }

    // Anti-enumeration guarantee: "unknown email", "email exists but has no password"
    // (Google-only account), and "wrong password" all produce the exact same
    // LoginOutcome.InvalidCredentials with no User attached — nothing here lets a caller
    // distinguish which case occurred. Verified by PasswordAuthServiceTests.
    public static async Task<LoginResult> LoginAsync(AppDbContext db, string email, string password)
    {
        var user = await FindByNormalizedEmailAsync(db, email);

        if (user is null || user.PasswordHash is null)
            return new LoginResult { Outcome = LoginOutcome.InvalidCredentials };

        var verification = new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
            return new LoginResult { Outcome = LoginOutcome.InvalidCredentials };

        if (user.EmailVerifiedAt is null)
            return new LoginResult { Outcome = LoginOutcome.NotVerified, User = user };

        return new LoginResult { Outcome = LoginOutcome.Success, User = user };
    }

    // Always resolvable to the same generic response by the caller regardless of what's
    // returned here — a null user means "don't send an email", not "tell the caller it
    // failed". Never distinguishes "no such email" from "email exists but is Google-only" to
    // the HTTP caller.
    public static async Task<(User? User, string? Token)> RequestPasswordResetAsync(AppDbContext db, string email)
    {
        var user = await FindByNormalizedEmailAsync(db, email);
        if (user is null || user.PasswordHash is null) return (null, null);

        var token = await UserVerificationTokenService.IssueAsync(db, user.Id, UserVerificationTokenPurpose.PasswordReset);
        return (user, token);
    }

    // Same email-casing normalization as UserProvisioningService.GetOrCreateAsync.
    private static Task<User?> FindByNormalizedEmailAsync(AppDbContext db, string email)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return db.Users.FirstOrDefaultAsync(u => u.Email == normalized);
    }

    public enum ResetPasswordOutcome
    {
        PasswordInvalid,
        InvalidToken,
        Success,
    }

    public class ResetPasswordResult
    {
        public required ResetPasswordOutcome Outcome { get; init; }
        public List<string> PasswordErrors { get; init; } = [];
        public User? User { get; init; }
    }

    public static async Task<ResetPasswordResult> ResetPasswordAsync(AppDbContext db, string token, string newPassword)
    {
        var passwordErrors = PasswordRules.Validate(newPassword);
        if (passwordErrors.Count > 0)
            return new ResetPasswordResult { Outcome = ResetPasswordOutcome.PasswordInvalid, PasswordErrors = passwordErrors };

        var user = await UserVerificationTokenService.ValidateAndConsumeAsync(db, token, UserVerificationTokenPurpose.PasswordReset);
        if (user is null)
            return new ResetPasswordResult { Outcome = ResetPasswordOutcome.InvalidToken };

        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, newPassword);
        await db.SaveChangesAsync();
        return new ResetPasswordResult { Outcome = ResetPasswordOutcome.Success, User = user };
    }
}
