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
    // The pre-existing single-user pipeline's token (JobSearchAgent/Integrations/GmailClient.cs)
    // — scoped to gmail.readonly, reads inbox content for application-status tracking.
    public const string GmailRefreshToken = "gmail_refresh_token";

    // GmailOAuthService's token — scoped to gmail.settings.basic only (filter/forwarding
    // management, cannot read mail content). Deliberately a separate key: a refresh token is
    // permanently locked to whatever scope it was originally granted, so if both features
    // shared one key, reconnecting Gmail for filters would silently downgrade — or connecting
    // for filters first would prevent ever granting — inbox-read access, and vice versa.
    public const string GmailSettingsRefreshToken = "gmail_settings_refresh_token";
}
