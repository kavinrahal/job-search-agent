using System.Text.RegularExpressions;

namespace JobSearch.Data;

// Shared Sentry setup for both JobSearch.Api and JobSearchAgent.
//
// This app processes resumes, free-text background YAML, and raw Gmail message bodies, so the
// scrubbing posture here is an allowlist, not a blocklist: everything Sentry would normally
// attach (request bodies, headers, cookies, local variable state, breadcrumb payloads, user
// email) is dropped, and only the exception type, stack trace, and a small set of explicitly
// safe tags survive. A blocklist of "sensitive field names" would be permanently one field
// behind the code — an allowlist fails closed instead.
//
// Free-text that we DO keep (the exception message) still gets pattern-redacted, since messages
// routinely interpolate user data ("no profile for kavin@example.com").
public static class SentryConfig
{
    // Deliberately narrow: an unanchored "looks like an email" and "looks like a long opaque
    // token" pass. Not trying to catch every possible PII shape in free text — the real
    // defense is that almost nothing free-text reaches Sentry in the first place.
    private static readonly Regex EmailPattern = new(
        @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // 24+ chars of unbroken base64/hex-ish text — API keys, tokens, refresh tokens, encrypted
    // secret blobs. Short words never match, so ordinary prose survives intact.
    private static readonly Regex OpaqueTokenPattern = new(
        @"\b[A-Za-z0-9_\-]{24,}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private const int MaxMessageLength = 2000;

    // Applied to any free-text string before it leaves the process.
    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        var redacted = EmailPattern.Replace(text, "[email]");
        redacted = OpaqueTokenPattern.Replace(redacted, "[redacted]");

        // A stack trace or a serialized payload can be enormous; cap it so one pathological
        // exception can't ship a whole resume through the message field.
        return redacted.Length > MaxMessageLength
            ? string.Concat(redacted.AsSpan(0, MaxMessageLength), "…[truncated]")
            : redacted;
    }

    // Console-app entry point (JobSearchAgent). SentrySdk.Init parses the DSN synchronously
    // and throws on a malformed value — with no handler around a top-level Main, that takes
    // the entire process down before it does anything else. Crash reporting must never be
    // able to crash the process it exists to monitor, so a bad DSN degrades to "no crash
    // reporting this run" instead of an outage. This is not a hypothetical: it happened in
    // production the first time SENTRY_DSN was set on the worker.
    public static IDisposable? TryInitConsole(string? dsn, string environment)
    {
        if (!IsEnabled(dsn)) return null;
        try
        {
            return SentrySdk.Init(o =>
            {
                o.Dsn = dsn!;
                o.Environment = environment;
                Harden(o);
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Sentry] Failed to initialize — continuing without crash reporting: {ex.Message}");
            return null;
        }
    }

    // True when Sentry should be wired up at all. Absent DSN = disabled, which is the normal
    // state locally and in tests — no dev machine should be shipping events to production
    // Sentry, and CI shouldn't either.
    public static bool IsEnabled(string? dsn) => !string.IsNullOrWhiteSpace(dsn);

    // Applies the shared hardening to whichever SDK is initializing (ASP.NET Core or the
    // plain worker). Callers set Dsn/Environment themselves; this only sets the parts that
    // must be identical across both processes.
    public static void Harden(SentryOptions options)
    {
        // Belt and braces: SendDefaultPii is already false by default, but it's the single
        // switch that would otherwise attach usernames, cookies, and client IPs, so it's
        // stated explicitly rather than left to the SDK default drifting in a future version.
        options.SendDefaultPii = false;
        options.AttachStacktrace = true;

        // No performance tracing — it burns event quota and this pipeline only cares about
        // crashes, not latency.
        options.TracesSampleRate = 0.0;

        options.SetBeforeSend(ScrubEvent);

        // Breadcrumbs record prior operations leading to the crash. The *shape* (which
        // operation) is useful; the payload can carry anything, so only the category and
        // message survive, redacted.
        options.SetBeforeBreadcrumb(crumb => new Breadcrumb(
            message: Redact(crumb.Message),
            type: crumb.Type ?? "",
            data: null,
            category: crumb.Category,
            level: crumb.Level));
    }

    // Exposed for tests — the whole security posture of this feature lives here, so it's
    // verified directly rather than through a live SDK round-trip.
    internal static SentryEvent? ScrubEvent(SentryEvent e)
    {
        // Request body/headers/cookies/query can contain a resume upload, a pasted job
        // description, or a session cookie. None of it is worth the exposure.
        e.Request.Data = null;
        e.Request.Headers.Clear();
        e.Request.Cookies = null;
        e.Request.QueryString = null;

        // Keep a stable per-user id so "how many users hit this" still works, but never the
        // email or IP that would identify who.
        var userId = e.User.Id;
        e.User.Email = null;
        e.User.IpAddress = null;
        e.User.Username = null;
        e.User.Other.Clear();
        e.User.Id = userId;

        // Extra is exposed read-only, so values are overwritten rather than removed. Keys are
        // developer-authored labels and safe to keep; the values are the risk.
        foreach (var key in e.Extra.Keys.ToList())
            e.SetExtra(key, "[redacted]");

        if (e.Message is { } msg)
            e.Message = new SentryMessage { Message = Redact(msg.Message ?? msg.Formatted) };

        foreach (var ex in e.SentryExceptions ?? [])
            ex.Value = Redact(ex.Value);

        return e;
    }
}
