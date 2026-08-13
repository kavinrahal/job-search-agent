namespace JobSearch.Data;

// Encrypted per-user secrets — Gmail refresh tokens today, BYO Anthropic keys later.
// App-level secrets shared by every user (Stripe key, SendGrid key, the app's own Google
// OAuth client id/secret) stay as env vars; this table is only for values that differ per
// user. EncryptedValue is never read/written except through UserSecretService.
public class UserSecret
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Key { get; set; } = "";
    public string EncryptedValue { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}

public static class UserSecretKey
{
    public const string GmailRefreshToken = "gmail_refresh_token";
}
