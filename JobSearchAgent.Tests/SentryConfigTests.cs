using JobSearch.Data;
using Sentry;
using Sentry.Protocol;

namespace JobSearchAgent.Tests;

// These guard the PII boundary for crash reporting. This app handles resumes, background
// YAML, and raw Gmail bodies, so a regression here leaks real user data to a third party —
// which makes these worth testing directly rather than trusting the SDK's defaults.
public class SentryConfigTests
{
    // TC01 — email addresses routinely get interpolated into error messages
    // ("no profile for x@y.com"); they must never leave the process.
    [Fact]
    public void Redact_StripsEmailAddresses()
    {
        var result = SentryConfig.Redact("UserProfile not found for kavin@example.com during fetch");

        Assert.DoesNotContain("kavin@example.com", result);
        Assert.Contains("[email]", result);
    }

    // TC02 — a leaked refresh token or API key in an exception message would be a live
    // credential sitting in a third-party dashboard.
    [Fact]
    public void Redact_StripsLongOpaqueTokens()
    {
        var result = SentryConfig.Redact("Token refresh failed for 1ffGOCSPXkqvWkr4cctXd4a7WLFXP9ZnZfzrpAAAA");

        Assert.DoesNotContain("1ffGOCSPXkqvWkr4cctXd4a7WLFXP9ZnZfzrpAAAA", result);
        Assert.Contains("[redacted]", result);
    }

    // TC03 — the cap is what stops a serialized resume or a whole email body riding out
    // inside one pathological exception message. Uses prose rather than one unbroken run of
    // characters, because that is what a dumped resume or email body actually looks like —
    // an unbroken run is caught by the opaque-token rule instead and never reaches the cap.
    [Fact]
    public void Redact_TruncatesOversizedText()
    {
        var resumeLikeText = string.Concat(Enumerable.Repeat("Software Engineer at Willow. ", 400));

        var result = SentryConfig.Redact(resumeLikeText);

        Assert.True(result.Length < resumeLikeText.Length);
        Assert.EndsWith("[truncated]", result);
    }

    // TC04 — the counterweight to TC01/TC02: over-aggressive redaction would destroy the
    // diagnostic value that justifies having crash reporting at all.
    [Fact]
    public void Redact_LeavesOrdinaryDiagnosticTextIntact()
    {
        const string message = "The given key 'rationale' was not present in the dictionary.";

        Assert.Equal(message, SentryConfig.Redact(message));
    }

    // TC05 — request bodies carry pasted job descriptions and uploaded resume content;
    // headers and cookies carry the session. None of it may ship.
    [Fact]
    public void ScrubEvent_ClearsRequestPayload()
    {
        var e = new SentryEvent();
        e.Request.Data = "{\"cvBase\":\"Kavin Abeysinghe, Software Engineer...\"}";
        e.Request.Headers["Cookie"] = "session=abc123";
        e.Request.QueryString = "?email=kavin@example.com";

        SentryConfig.ScrubEvent(e);

        Assert.Null(e.Request.Data);
        Assert.Empty(e.Request.Headers);
        Assert.Null(e.Request.QueryString);
    }

    // TC06 — "how many users hit this" must still work, so the opaque id survives while
    // everything that identifies the person does not.
    [Fact]
    public void ScrubEvent_KeepsUserIdButDropsIdentifyingFields()
    {
        var e = new SentryEvent();
        e.User.Id = "42";
        e.User.Email = "kavin@example.com";
        e.User.IpAddress = "203.0.113.7";
        e.User.Username = "kavinrahal";

        SentryConfig.ScrubEvent(e);

        Assert.Equal("42", e.User.Id);
        Assert.Null(e.User.Email);
        Assert.Null(e.User.IpAddress);
        Assert.Null(e.User.Username);
    }

    // TC07 — the exception message is the one free-text field we deliberately keep, so the
    // redaction pass has to actually be wired into the event path, not just exist.
    [Fact]
    public void ScrubEvent_RedactsExceptionMessages()
    {
        var e = new SentryEvent
        {
            SentryExceptions = [new SentryException { Value = "Gmail fetch failed for kavin@example.com" }],
        };

        SentryConfig.ScrubEvent(e);

        var scrubbed = e.SentryExceptions!.Single().Value;
        Assert.DoesNotContain("kavin@example.com", scrubbed);
        Assert.Contains("[email]", scrubbed);
    }
}
