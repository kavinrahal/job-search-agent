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
}

// Bare entitlement fields — Stripe/billing wiring is a separate later ticket.
public static class UserTier
{
    public const string Tier1 = "Tier1";
    public const string Tier2 = "Tier2";
}
