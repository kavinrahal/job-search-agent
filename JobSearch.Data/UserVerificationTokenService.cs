using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Data;

// Issues and validates the opaque tokens backing email verification and password reset
// (UserVerificationToken — one table for both, see that file). Short-lived (~1 hour) and
// single-use, unlike InboundEmailService's long-lived forwarding-address token.
public static class UserVerificationTokenService
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(1);

    // Same "opaque, unguessable, RandomNumberGenerator not Guid.NewGuid" idiom as
    // InboundEmailService.GenerateToken — 20 bytes (160 bits) here since these gate account
    // takeover/password-reset, not just an inbound-mail routing address.
    public static string GenerateToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(20)).ToLowerInvariant();

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    // Generates a token, persists only its hash, and returns the raw token for the caller to
    // email — the raw value never touches the database.
    public static async Task<string> IssueAsync(AppDbContext db, int userId, string purpose)
    {
        var token = GenerateToken();
        db.UserVerificationTokens.Add(new UserVerificationToken
        {
            UserId = userId,
            TokenHash = Hash(token),
            Purpose = purpose,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(Ttl),
        });
        await db.SaveChangesAsync();
        return token;
    }

    // Validates and, in the same call, consumes the token so it can never be replayed.
    // Returns null for any invalid state (unknown hash, wrong purpose, expired, already
    // consumed) without distinguishing which — callers surface one generic error either way.
    public static async Task<User?> ValidateAndConsumeAsync(AppDbContext db, string token, string purpose)
    {
        var hash = Hash(token);
        var record = await db.UserVerificationTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.Purpose == purpose);

        if (record is null || record.ConsumedAt is not null || record.ExpiresAt < DateTime.UtcNow)
            return null;

        var user = await db.Users.FindAsync(record.UserId);
        if (user is null) return null;

        record.ConsumedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return user;
    }
}
