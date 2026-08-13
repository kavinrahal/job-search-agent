namespace JobSearch.Data;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string Tier { get; set; } = UserTier.Tier1;
    public int CreditBalance { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Bare entitlement fields — Stripe/billing wiring is a separate later ticket.
public static class UserTier
{
    public const string Tier1 = "Tier1";
    public const string Tier2 = "Tier2";
}
