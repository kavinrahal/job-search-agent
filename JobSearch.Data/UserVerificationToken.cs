namespace JobSearch.Data;

// One table for both email-verification and password-reset tokens — structurally identical
// (a token tied to a user, an expiry, a single use), so one shape covers both rather than two
// near-duplicate tables. Unlike InboundEmailService.InboundEmailToken (long-lived, stored raw
// and looked up directly), these are short-lived and security-sensitive, so only a hash of
// the token is ever persisted — a DB read alone can't produce a usable token.
public class UserVerificationToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public string Purpose { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    // Null until used. Prevents replay — once consumed, the same raw token can never
    // validate again even if it hasn't expired yet.
    public DateTime? ConsumedAt { get; set; }
}

// Values for UserVerificationToken.Purpose. A plain string constant set, matching this
// codebase's existing convention for closed value sets (see UserTier, GmailTrackingMode)
// rather than a native enum column.
public static class UserVerificationTokenPurpose
{
    public const string EmailVerification = "EmailVerification";
    public const string PasswordReset = "PasswordReset";
}
