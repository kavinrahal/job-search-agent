using System.Security.Cryptography;
using System.Text;
using JobSearch.Data;

namespace JobSearchAgent.Tests;

// The triage gate decides whether a full Claude Code session gets spent, and the verifier is
// the only thing standing between the public internet and an agent with push access. Both
// are worth testing directly.
public class CrashTriageTests
{
    private const int Cap = 3;

    // TC01 — the case the whole feature exists for.
    [Fact]
    public void Evaluate_NewActionableError_Dispatches()
    {
        var d = CrashTriage.Evaluate("error", "NullReferenceException in GenerateArtifactAsync",
            alreadyDispatched: false, dispatchesInLastHour: 0, Cap);

        Assert.True(d.ShouldDispatch);
    }

    // TC02 — webhook delivery retries and manual replays both happen; neither may re-run the
    // agent and open a duplicate PR.
    [Fact]
    public void Evaluate_AlreadyDispatched_Skips()
    {
        var d = CrashTriage.Evaluate("error", "NullReferenceException",
            alreadyDispatched: true, dispatchesInLastHour: 0, Cap);

        Assert.False(d.ShouldDispatch);
    }

    // TC03 — warning/info issues are diagnostics, not crashes; paying for a fix run on one
    // would be pure waste.
    [Theory]
    [InlineData("warning")]
    [InlineData("info")]
    public void Evaluate_NonErrorLevels_Skip(string level)
    {
        var d = CrashTriage.Evaluate(level, "Something noteworthy",
            alreadyDispatched: false, dispatchesInLastHour: 0, Cap);

        Assert.False(d.ShouldDispatch);
    }

    // TC04 — the job boards 403 datacenter IPs as policy and the pipeline already falls back
    // gracefully. No code change on our side fixes it, so it must never wake the agent.
    [Fact]
    public void Evaluate_KnownThirdPartyNoise_Skips()
    {
        var d = CrashTriage.Evaluate("error",
            "HttpRequestException: Response status code does not indicate success: 403 (Forbidden).",
            alreadyDispatched: false, dispatchesInLastHour: 0, Cap);

        Assert.False(d.ShouldDispatch);
    }

    // TC05 — the burst guard. A bad deploy produces many *distinct* new issues at once, which
    // Sentry's own per-issue throttling cannot limit; without this it becomes N concurrent
    // agent runs and N times the spend.
    [Fact]
    public void Evaluate_AtHourlyCap_Skips()
    {
        var d = CrashTriage.Evaluate("error", "NullReferenceException",
            alreadyDispatched: false, dispatchesInLastHour: Cap, Cap);

        Assert.False(d.ShouldDispatch);
    }

    // TC06 — a valid Sentry signature is accepted, or the feature simply never fires.
    [Fact]
    public void Verifier_ValidSignature_Accepted()
    {
        const string secret = "s3cr3t";
        var body = "{\"action\":\"created\"}"u8.ToArray();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signature = Convert.ToHexStringLower(hmac.ComputeHash(body));

        Assert.True(SentryWebhookVerifier.IsValid(signature, body, secret));
    }

    // TC07 — the actual attack: anyone who can POST to the endpoint could otherwise burn
    // tokens and feed arbitrary text into an agent that holds push access.
    [Fact]
    public void Verifier_WrongSignature_Rejected()
    {
        var body = "{\"action\":\"created\"}"u8.ToArray();

        Assert.False(SentryWebhookVerifier.IsValid(new string('a', 64), body, "s3cr3t"));
    }

    // TC08 — a tampered body must invalidate a signature that was valid for the original,
    // which is the property that stops replay-with-edits.
    [Fact]
    public void Verifier_TamperedBody_Rejected()
    {
        const string secret = "s3cr3t";
        var original = "{\"action\":\"created\"}"u8.ToArray();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signature = Convert.ToHexStringLower(hmac.ComputeHash(original));

        var tampered = "{\"action\":\"deleted\"}"u8.ToArray();

        Assert.False(SentryWebhookVerifier.IsValid(signature, tampered, secret));
    }

    // TC09 — an unconfigured secret must fail closed. Failing open here would leave the
    // endpoint wide open during the window before the secret is set in Railway.
    [Fact]
    public void Verifier_MissingSecret_Rejected()
    {
        var body = "{}"u8.ToArray();

        Assert.False(SentryWebhookVerifier.IsValid("anything", body, null));
    }
}
