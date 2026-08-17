using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Data;

// Backs the per-user opaque forwarding address used by SendGrid's Inbound Parse pipeline
// (Tier 2 job-alert forwarding — see the webhook in JobSearch.Api/Program.cs). A user
// forwards platform alert emails (Seek, LinkedIn, Jora) to their own address under a
// domain we control; SendGrid POSTs each message to one shared webhook, which uses the
// address's token to work out whose mail it is.
public static class InboundEmailService
{
    // 20 lowercase hex chars (80 bits) — opaque and unguessable per the security
    // checklist, not a short or sequential id a scanner could enumerate.
    public static string GenerateToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(10)).ToLowerInvariant();

    // Returns the user's full forwarding address, generating and persisting a token on
    // first call — most users never need one, so it's created lazily rather than at signup.
    public static async Task<string> GetOrCreateAddressAsync(AppDbContext db, int userId, string domain)
    {
        var user = await db.Users.FindAsync(userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        if (user.InboundEmailToken is null)
        {
            user.InboundEmailToken = GenerateToken();
            await db.SaveChangesAsync();
        }

        return $"{user.InboundEmailToken}@{domain}";
    }

    // Resolves the owning user's id from the raw "To" value SendGrid posts (e.g.
    // "abc123@alerts.example.com" or "Name <abc123@alerts.example.com>") — null if the
    // token doesn't match any user (stale address, or something hitting the wildcard
    // domain directly rather than a real per-user address).
    public static async Task<int?> ResolveUserIdAsync(AppDbContext db, string toHeader)
    {
        var token = ExtractToken(toHeader);
        if (token is null) return null;

        return await db.Users.Where(u => u.InboundEmailToken == token).Select(u => (int?)u.Id).FirstOrDefaultAsync();
    }

    private static string? ExtractToken(string toHeader)
    {
        var at = toHeader.IndexOf('@');
        if (at <= 0) return null;

        // Strip a leading "Name <" if present, e.g. "Name <abc123@domain>".
        var start = toHeader.LastIndexOf('<', at) + 1;
        var token = toHeader[start..at].Trim();
        return token.Length > 0 ? token : null;
    }
}
