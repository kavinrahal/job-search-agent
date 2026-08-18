namespace JobSearch.Data;

// An email the owner has explicitly invited to sign up during the beta. Presence in this
// table is what BetaAccessService checks (alongside the hardcoded owner email and any
// already-existing account) to decide whether a Google sign-in is allowed to create a new
// account at all — and being invited always lands them straight at Tier 2, since that's the
// entire point of this table: the owner is personally choosing who gets Tier 2 access.
public class BetaInvite
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public DateTime InvitedAt { get; set; }
}
