using System.Text;
using System.Text.Json;
using JobSearch.Data;

namespace JobSearchAgent.Tests;

// Regression eval answering a specific question: now that CvTailorAgent fires 3 smaller
// parallel tool-use calls emitting only a delta (instead of the old single free-text call that
// re-emitted the whole CV), does that shape change already fix the truncation failure mode that
// caused the 2026-08-19 production incident (a resume cut off mid-sentence in the Summary) --
// independent of which model runs it? Runs every fixture in Fixtures/CvTailorEvalFixtures.cs
// against both Opus (production default) and Sonnet, using CvTailorAgent's real call shape
// (GenerateRawAsync -- the same 3 parallel CallAsync invocations GenerateAsync uses, split out
// purely so this eval can inspect each call's raw tool-use output independently rather than only
// the merged, rendered document -- see CvTailorAgent.GenerateRawAsync).
//
// HOW TO RUN (contract test: makes real, billed Claude API calls -- excluded from the default
// `dotnet test` / CI run via ci.yml's `--filter "Category!=contract"`, same convention as
// EmailClassifierEvalTests and ContractTests.cs):
//
//   ANTHROPIC_API_KEY=sk-... dotnet test --filter "FullyQualifiedName~CvTailorAgentModelEvalTests"
//
// Two separate [Fact]s (one per model) so xunit reports Opus's and Sonnet's results as distinct
// pass/fail outcomes rather than one combined result. Each prints a full per-fixture report
// (visible with `dotnet test ... --logger "console;verbosity=detailed"`) -- including the raw
// generated text for every call -- so a human can additionally read Opus's and Sonnet's output
// side by side for the secondary, subjective "is the tailoring comparably good" question this
// harness can't itself judge (grounding, hallucination, tailoring quality).
//
// WHAT COUNTS AS A FAILURE (the primary, objective signal -- see class comment above; this is
// deliberately not a prose-quality check):
//   1. GenerateRawAsync throws -- CvTailorAgent's own ClaudeToolCallRetry wrapper already retries
//      once on a missing/malformed tool-use block and only then throws, so a thrown exception
//      here means a call never produced a usable structured response even after that retry.
//   2. A required field comes back near-empty/single-word -- the shape the 2026-08-19 incident's
//      worst case took ("To" as an entire cover letter). `summary` is the only field a
//      first-generation call is required by tailor_cv.md to always produce fresh content for
//      (rule 1: "Always replace it with a fresh summary specific to this role") -- an empty
//      experience_overrides/project_overrides array is legitimate ("omit an entry if nothing
//      about it changes"), so this only flags *present* override entries whose own text content
//      (text_override / company_description_override / description_override) is near-empty.
//
// NOT CHECKED (documented limitation, not an oversight): stop_reason. ClaudeToolCallRetry (shared
// infra used by several agents, out of this eval's scope) doesn't surface the raw response's
// StopReason to its caller today, only the parsed tool input -- so this eval can't distinguish
// "hit max_tokens" from "the model just produced thin content" as the cause of a near-empty
// field. Both are covered by the same near-empty check either way; only the *reason* is unknown.
public class CvTailorAgentModelEvalTests
{
    private static string? ApiKey => Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

    private const string OpusModelId = "claude-opus-4-8";
    // Mirrors the literal used in the 2026-08-19 incident and its revert (see CvTailorAgent's and
    // CoverLetterAgent's model-constant comments) -- not a production constant, since this eval is
    // the only place in the codebase that currently needs to name Sonnet 5 by id.
    private const string SonnetModelId = "claude-sonnet-5";

    // A required field is "near-empty" below this length/word count -- calibrated well under any
    // real tailored sentence (tailor_cv.md's summary rule alone asks for 2-3 full sentences), but
    // comfortably above what a truncation/degenerate-output artifact like "To" would produce.
    private const int MinFieldChars = 20;
    private const int MinFieldWords = 4;

    private static bool IsNearEmpty(string? text) =>
        string.IsNullOrWhiteSpace(text) ||
        text.Trim().Length < MinFieldChars ||
        text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < MinFieldWords;

    private static UserResume EmptyBaseResume(int userId) => new()
    {
        UserId = userId,
        Summary = "",
        SectionConfigJson = "[]",
        ExperienceOverridesJson = "[]",
        SkillsSectionJson = "[]",
        ProjectOverridesJson = "[]",
    };

    private record FixtureResult(string FixtureId, List<string> Failures, string RawSummary, string RawExperience, string RawProjects);

    private static async Task<(List<FixtureResult> Results, string Report)> RunFixtureSuiteAsync(string model)
    {
        var agent = new CvTailorAgent(ApiKey!, model: model);
        var results = new List<FixtureResult>();
        var sb = new StringBuilder();

        foreach (var fixture in CvTailorEvalFixtures.All)
        {
            var failures = new List<string>();
            string rawSummary = "", rawExperience = "", rawProjects = "";

            var profile = new UserProfile { UserId = 1, Background = fixture.BackgroundYaml };
            var resume = EmptyBaseResume(profile.UserId);
            var background = BackgroundYamlParser.Parse(fixture.BackgroundYaml);

            try
            {
                var (summarySkills, experience, projects) = await agent.GenerateRawAsync(background, profile, resume, fixture.PostingText, fixture.EvaluationJson);

                rawSummary = JsonSerializer.Serialize(summarySkills);
                rawExperience = JsonSerializer.Serialize(experience);
                rawProjects = JsonSerializer.Serialize(projects);

                var summary = summarySkills.TryGetValue("summary", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
                if (IsNearEmpty(summary))
                    failures.Add($"summary near-empty ({(summary is null ? "missing" : $"\"{summary}\"")})");

                CheckOverrideEntries(experience, "experience_overrides", "experience_index", failures);
                CheckOverrideEntries(projects, "project_overrides", "project_index", failures);
            }
            catch (Exception ex)
            {
                failures.Add($"GenerateRawAsync threw: {ex.GetType().Name}: {ex.Message}");
            }

            results.Add(new FixtureResult(fixture.Id, failures, rawSummary, rawExperience, rawProjects));

            sb.AppendLine($"  [{fixture.Id}] {(failures.Count == 0 ? "OK" : "FAIL")}");
            if (failures.Count > 0)
                foreach (var f in failures) sb.AppendLine($"      - {f}");
            sb.AppendLine($"      summary_and_skills: {Truncate(rawSummary, 300)}");
            sb.AppendLine($"      experience_overrides: {Truncate(rawExperience, 300)}");
            sb.AppendLine($"      project_overrides: {Truncate(rawProjects, 300)}");
        }

        int failCount = results.Count(r => r.Failures.Count > 0);
        var header = $"CvTailorAgent model eval -- model={model} -- {results.Count - failCount}/{results.Count} fixtures clean\n";
        return (results, header + sb);
    }

    // Shared by experience_overrides and project_overrides: both carry an ItemOverride-shaped
    // achievements/highlights array plus a description-style override string (see
    // ResumeOverrideSchema). Only inspects entries the model actually returned -- an empty array
    // is a legitimate "nothing to tailor" result, not a failure (see class comment).
    private static void CheckOverrideEntries(IReadOnlyDictionary<string, JsonElement> input, string arrayKey, string indexKey, List<string> failures)
    {
        if (!input.TryGetValue(arrayKey, out var arr) || arr.ValueKind != JsonValueKind.Array) return;

        foreach (var entry in arr.EnumerateArray())
        {
            var index = entry.TryGetProperty(indexKey, out var idx) ? idx.ToString() : "?";

            foreach (var descField in new[] { "company_description_override", "description_override" })
            {
                if (entry.TryGetProperty(descField, out var d) && d.ValueKind == JsonValueKind.String && IsNearEmpty(d.GetString()))
                    failures.Add($"{arrayKey}[{indexKey}={index}].{descField} near-empty (\"{d.GetString()}\")");
            }

            foreach (var itemsField in new[] { "achievements", "highlights" })
            {
                if (!entry.TryGetProperty(itemsField, out var items) || items.ValueKind != JsonValueKind.Array) continue;
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("text_override", out var t) && t.ValueKind == JsonValueKind.String && IsNearEmpty(t.GetString()))
                        failures.Add($"{arrayKey}[{indexKey}={index}].{itemsField}.text_override near-empty (\"{t.GetString()}\")");
                }
            }
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "...";

    [Fact]
    [Trait("Category", "contract")]
    public async Task CvTailorAgent_Opus_NoTruncationAcrossFixtures()
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
    public async Task CvTailorAgent_Sonnet_NoTruncationAcrossFixtures()
    {
        if (ApiKey is null) return;

        var (results, report) = await RunFixtureSuiteAsync(SonnetModelId);
        Console.WriteLine(report);

        var failed = results.Where(r => r.Failures.Count > 0).ToList();
        Assert.True(failed.Count == 0,
            $"{failed.Count}/{results.Count} fixtures failed on Sonnet -- see console output above for detail. " +
            "Per the model-swap decision rule: even one reproduction here means CvTailorAgent should stay on Opus.");
    }
}
