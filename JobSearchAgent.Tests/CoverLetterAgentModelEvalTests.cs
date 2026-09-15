using System.Text;
using JobSearch.Data;

namespace JobSearchAgent.Tests;

// Regression eval for CoverLetterAgent, the safeguarded counterpart to CvTailorAgentModelEvalTests
// (read that class's header first — same methodology, same run instructions, same contract-test
// exclusion from CI). The question here is narrower and, unlike CvTailorAgent, was NOT answerable
// by a shape change: CoverLetterAgent is still a single free-text call (a letter has no schema to
// force into smaller tool-use deltas), so the 2026-08-19 truncation/degenerate failure mode (a
// letter whose entire body was the word "To") could recur on any model. What changed is that
// CoverLetterAgent.GenerateAsync/ReviseAsync now wrap the call in an output safeguard
// (CallWithSafeguardAsync): a response with an abnormal stop_reason or a suspiciously short body
// is retried once, and only a persistently-bad response is returned for the API-layer gate to
// reject. This eval asks: with that safeguard active, does Sonnet reliably produce a valid cover
// letter across varied candidate backgrounds — i.e. is it now safe to switch this agent's default
// off Opus?
//
// HOW TO RUN (real, billed Claude API calls — excluded from the default `dotnet test` / CI run via
// ci.yml's `--filter "Category!=contract"`, same as CvTailorAgentModelEvalTests / ContractTests):
//
//   ANTHROPIC_API_KEY=sk-... dotnet test --filter "FullyQualifiedName~CoverLetterAgentModelEvalTests"
//
// Reuses Fixtures/CvTailorEvalFixtures rather than a second fixture file: those fixtures already
// carry exactly what CoverLetterAgent.GenerateAsync consumes (Background + posting + evaluation
// JSON — a cover letter needs no UserResume), and they already span the varied/edge-case
// backgrounds this eval wants (short, long-and-dense, sparse/early-career, career changer, unicode,
// employment gap, unusual sections). All are substantive, matchable backgrounds on purpose: a
// near-empty background legitimately produces a refusal, which is correct behaviour handled by the
// API-layer gate, not the truncation/degeneracy bug this eval is measuring.
//
// WHAT COUNTS AS A FAILURE (objective — this is not a prose-quality judgement):
//   1. GenerateAsync throws — even the safeguard's retry couldn't get a usable response.
//   2. The returned letter fails CoverLetterOutputValidator.LooksLikeCoverLetter — the same
//      authoritative gate Program.cs applies before persisting/serving: catches the degenerate
//      shapes (missing salutation, out-of-range length incl. the truncated/one-word case, refusal
//      narration). A failure here means the safeguard's single retry was not enough to recover a
//      valid letter.
//
// The per-fixture report also surfaces any `[CoverLetterAgent] ... sanity check` lines the
// safeguard wrote to stderr during the run, so a human can see whether the safeguard had to fire
// at all (the lead's "safeguard held up / wasn't needed" question) and read each letter to judge
// the subjective quality this harness can't.
public class CoverLetterAgentModelEvalTests
{
    private static string? ApiKey => Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

    // The Opus baseline (current production default), exercised via CoverLetterAgent's model
    // override; Sonnet is the candidate this eval is deciding on.
    private const string OpusModelId = "claude-opus-4-8";
    private const string SonnetModelId = "claude-sonnet-5";

    private record FixtureResult(string FixtureId, List<string> Failures, string SafeguardLog, string Letter);

    private static async Task<(List<FixtureResult> Results, string Report)> RunFixtureSuiteAsync(string model)
    {
        var agent = new CoverLetterAgent(ApiKey!, model: model);
        var results = new List<FixtureResult>();
        var sb = new StringBuilder();

        foreach (var fixture in CvTailorEvalFixtures.All)
        {
            var failures = new List<string>();
            string letter = "";

            var profile = new UserProfile { UserId = 1, Background = fixture.BackgroundYaml };

            // Capture the safeguard's own stderr lines for this one call so the report can show
            // whether a retry fired. Safe from interleaving: the two [Fact]s below live in one test
            // class (one xunit collection) and so never run concurrently, and each runs its
            // fixtures sequentially.
            var originalErr = Console.Error;
            var capturedErr = new StringWriter();
            Console.SetError(capturedErr);
            try
            {
                letter = await agent.GenerateAsync(profile, fixture.PostingText, fixture.EvaluationJson);
            }
            catch (Exception ex)
            {
                failures.Add($"GenerateAsync threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                Console.SetError(originalErr);
            }

            if (failures.Count == 0 && !CoverLetterOutputValidator.LooksLikeCoverLetter(letter))
                failures.Add($"output failed LooksLikeCoverLetter ({DescribeInvalidLetter(letter)})");

            var safeguardLog = capturedErr.ToString().Trim();
            results.Add(new FixtureResult(fixture.Id, failures, safeguardLog, letter));

            sb.AppendLine($"  [{fixture.Id}] {(failures.Count == 0 ? "OK" : "FAIL")}");
            foreach (var f in failures) sb.AppendLine($"      - {f}");
            if (safeguardLog.Length > 0)
                foreach (var line in safeguardLog.Split('\n'))
                    sb.AppendLine($"      safeguard: {line.Trim()}");
            sb.AppendLine($"      letter: {Truncate(letter.Replace('\n', ' '), 300)}");
        }

        int failCount = results.Count(r => r.Failures.Count > 0);
        var header = $"CoverLetterAgent model eval -- model={model} -- {results.Count - failCount}/{results.Count} fixtures clean\n";
        return (results, header + sb);
    }

    // Report-only breakdown of why LooksLikeCoverLetter rejected a letter — mirrors that method's
    // own checks so the console output points at the specific degenerate shape.
    private static string DescribeInvalidLetter(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "empty/whitespace";
        var reasons = new List<string>();
        var trimmed = text.Trim();
        var wordCount = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount < CoverLetterOutputValidator.MinWords) reasons.Add($"too short: {wordCount} words");
        if (wordCount > CoverLetterOutputValidator.MaxWords) reasons.Add($"too long: {wordCount} words");
        var lower = trimmed.ToLowerInvariant();
        if (!lower.StartsWith("dear ") && !lower.StartsWith("to the hiring manager,")) reasons.Add("no valid salutation");
        return reasons.Count > 0 ? string.Join("; ", reasons) : "refusal-signal phrase or other";
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "...";

    [Fact]
    [Trait("Category", "contract")]
    public async Task CoverLetterAgent_Opus_ValidLettersAcrossFixtures()
    {
        if (ApiKey is null) return;

        var (results, report) = await RunFixtureSuiteAsync(OpusModelId);
        Console.WriteLine(report);

        var failed = results.Where(r => r.Failures.Count > 0).ToList();
        Assert.True(failed.Count == 0,
            $"{failed.Count}/{results.Count} fixtures failed on Opus -- see console output above for detail.");
    }

    [Fact]
    [Trait("Category", "contract")]
    public async Task CoverLetterAgent_Sonnet_ValidLettersAcrossFixtures()
    {
        if (ApiKey is null) return;

        var (results, report) = await RunFixtureSuiteAsync(SonnetModelId);
        Console.WriteLine(report);

        var failed = results.Where(r => r.Failures.Count > 0).ToList();
        Assert.True(failed.Count == 0,
            $"{failed.Count}/{results.Count} fixtures failed on Sonnet -- see console output above for detail. " +
            "Per the model-swap decision rule: even one unrecovered bad letter here means CoverLetterAgent should stay on Opus.");
    }
}
