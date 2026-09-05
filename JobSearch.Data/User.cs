namespace JobSearch.Data;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string Tier { get; set; } = UserTier.Tier1;
    public int CreditBalance { get; set; }

    // Optimistic concurrency token for CreditBalance writes (see CreditService.SpendCreditAsync)
    // — not a general row version, just enough to make two simultaneous spends against the
    // same balance detectable instead of silently overwriting each other.
    public int CreditVersion { get; set; }

    public DateTime CreatedAt { get; set; }

    // Soft-deactivation, not a hard delete — set on account cancellation, checked at login
    // to block sign-in, so a returning user's data is still there if they come back. Null
    // means active.
    public DateTime? DeactivatedAt { get; set; }

    // CSV of JobSource keys chosen in the Tier 2 "choose your sources" step. Null means the
    // step hasn't been completed yet — Tier 1 users never see it, so stays null for them.
    public string? EnabledSources { get; set; }

    // Opaque local-part of this user's SendGrid inbound-forwarding address (see
    // InboundEmailService). Null until first requested — most users never need one.
    public string? InboundEmailToken { get; set; }

    // How this user wants application-status tracking to work — see GmailTrackingMode.
    // Null means not chosen yet (same convention as EnabledSources): no default is ever
    // silently assumed, since "full" implies reading inbox content.
    public string? GmailTrackingMode { get; set; }

    // Email/password login, fully independent of Google OAuth. Null means "no password set,
    // Google-only" — the state of every user before this feature and of every Google user
    // indefinitely afterward; nobody is ever migrated or prompted to set one. Only set when
    // someone registers via POST /auth/register or completes POST /auth/reset-password.
    public string? PasswordHash { get; set; }

    // Set the moment this email first proves ownership: automatically on first successful
    // Google login (Google already did the proving), or on completing the emailed
    // verification link from password registration. Password login is gated on this being
    // non-null; Google login never reads or is affected by it. Also what makes the
    // account-linking safety net work — a password registration against an email that
    // already has this set (a prior Google login) skips re-verification entirely.
    public DateTime? EmailVerifiedAt { get; set; }

    // Set by the worker's per-user loop (JobSearchAgent/Program.cs) the moment a
    // TokenResponseException proves this user's GmailRefreshToken has been revoked or
    // expired — same nullable-DateTime "state" convention as DeactivatedAt above (null means
    // fine). Two jobs: (1) excludes the user from the activeUsers query so every subsequent
    // cron run doesn't keep re-attempting a Gmail call already known to fail, and (2) gates
    // the one-time "please reconnect Gmail" email — only sent on the run that first sets
    // this, not on every run while it stays set. Cleared by the /gmail-oauth/callback
    // reconnect flow in JobSearch.Api/Program.cs once a fresh refresh token is stored.
    public DateTime? GmailConnectionBrokenAt { get; set; }
}

// Bare entitlement fields — Stripe/billing wiring is a separate later ticket.
public static class UserTier
{
    public const string Tier1 = "Tier1";
    public const string Tier2 = "Tier2";
}

// Values for User.GmailTrackingMode.
public static class GmailTrackingMode
{
    public const string Full   = "full";   // gmail.readonly — reads inbox content
    public const string Filter = "filter"; // gmail.settings.basic — per-company forwarding filters only
    public const string Manual = "manual"; // no Gmail integration for this feature

    public static readonly HashSet<string> All = [Full, Filter, Manual];
}
