using System.Security.Cryptography;
using System.Text;

namespace AdminDashboard.Api.Services;

// Verifies a /Login submission against the two shared AdminPortalUsername/AdminPortalPassword
// env vars. Both are compared with a constant-time equality check, same pattern as
// JobSearch.Data's SentryWebhookVerifier — a plain string comparison would leak either value one
// byte at a time to anyone measuring response times, same reasoning as that file's own comment.
// Both fields must match; a mismatch on either one gives the same generic failure, so a submitted
// username alone can't be used to probe whether it's the configured one.
public static class AdminCredentialVerifier
{
    public static bool IsValid(
        string? submittedUsername, string? submittedPassword,
        string? configuredUsername, string? configuredPassword)
    {
        // Nothing configured, or nothing submitted, means the endpoint cannot be trusted at
        // all — reject rather than fall open (same stance as SentryWebhookVerifier).
        if (string.IsNullOrEmpty(configuredUsername) || string.IsNullOrEmpty(configuredPassword)
            || string.IsNullOrEmpty(submittedUsername) || string.IsNullOrEmpty(submittedPassword))
            return false;

        bool usernameMatches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(submittedUsername), Encoding.UTF8.GetBytes(configuredUsername));
        bool passwordMatches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(submittedPassword), Encoding.UTF8.GetBytes(configuredPassword));

        // Both checks always run — no short-circuit `&&` — so a wrong username doesn't skip the
        // password comparison and shave a measurable amount of time off the response.
#pragma warning disable S2178 // deliberate non-short-circuit `&`, see comment above
        return usernameMatches & passwordMatches;
#pragma warning restore S2178
    }
}
