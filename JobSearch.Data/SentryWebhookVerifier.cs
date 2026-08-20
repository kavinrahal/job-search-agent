using System.Security.Cryptography;
using System.Text;

namespace JobSearch.Data;

// Verifies that a webhook actually came from Sentry.
//
// This endpoint is the trigger for an agent that can push branches and open PRs, so an
// unauthenticated caller here would be able to spend tokens at will and feed arbitrary text
// into an agent prompt. Sentry signs every Internal Integration webhook with HMAC-SHA256 of
// the raw request body, keyed on the integration's client secret.
public static class SentryWebhookVerifier
{
    public static bool IsValid(string? signatureHeader, byte[] rawBody, string? clientSecret)
    {
        // No secret configured means the endpoint cannot be trusted at all, so it rejects
        // rather than falling open.
        if (string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(clientSecret));
        var computed = Convert.ToHexStringLower(hmac.ComputeHash(rawBody));

        // Fixed-time compare — a plain string equality here leaks the expected signature one
        // byte at a time to anyone willing to measure.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(signatureHeader));
    }
}
