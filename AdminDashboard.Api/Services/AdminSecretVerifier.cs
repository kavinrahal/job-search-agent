using System.Security.Cryptography;
using System.Text;

namespace AdminDashboard.Api.Services;

// Verifies the /Login submission against the one shared AdminPortalSecret env var. Same
// constant-time-compare pattern as JobSearch.Data's SentryWebhookVerifier — a plain string
// equality here would leak the configured secret one byte at a time to anyone measuring
// response times, same reasoning as that file's own comment.
public static class AdminSecretVerifier
{
    public static bool IsValid(string? submitted, string? configuredSecret)
    {
        // No secret configured, or nothing submitted, means the endpoint cannot be trusted
        // at all — reject rather than fall open (same stance as SentryWebhookVerifier).
        if (string.IsNullOrEmpty(configuredSecret) || string.IsNullOrEmpty(submitted))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(submitted),
            Encoding.UTF8.GetBytes(configuredSecret));
    }
}
