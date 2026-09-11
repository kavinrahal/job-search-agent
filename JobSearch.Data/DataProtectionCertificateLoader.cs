using System.Security.Cryptography.X509Certificates;

namespace JobSearch.Data;

// Loads the certificate used to wrap the Data Protection key ring at rest, shared by both
// JobSearch.Api and JobSearchAgent (see the AddDataProtection() setup in each Program.cs). The
// key ring itself is persisted to Postgres in both processes; without this wrapping, a DB
// compromise alone yields the keys that decrypt every session cookie, every stored Gmail
// UserSecret, and the CSRF cookie. Extracted out of Program.cs (rather than left inline) purely
// so the conditional-vs-absent behavior has a direct unit test.
public static class DataProtectionCertificateLoader
{
    // Null when either env var is missing — the normal state locally and in production today
    // (this is a staging-first rollout; see DP_CERT_PFX_B64 / DP_CERT_PFX_PASSWORD on Railway).
    // Callers must treat null as "feature off": keep persisting the key ring unprotected exactly
    // as before, not fail startup. Both processes must be called with the same two values, since
    // they share one key ring — a cert wired into only one process would make it unable to read
    // keys the other one protects.
    //
    // A non-empty-but-malformed value (bad base64, wrong password, corrupt PFX) is deliberately
    // left to throw rather than degrading to "feature off" — that combination only happens once
    // someone has actually set the vars, so a bad cert is a real setup error worth failing
    // startup loudly over, not silently masking as unprotected.
    public static X509Certificate2? TryLoad(string? pfxBase64, string? password)
    {
        if (string.IsNullOrEmpty(pfxBase64) || string.IsNullOrEmpty(password))
            return null;

        return X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(pfxBase64), password);
    }
}
