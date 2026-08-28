using System.Text.Json;
using JobSearch.Data;

namespace JobSearchAgent.Tests;

// Exercises PostingEvaluator.ParseEvaluation directly against a fabricated tool-use input, so the
// literal-"null"-string normalization can be verified deterministically without a live LLM call.
// See DiscoveriesPage "Disqualifier: null" / "Salary: null" bug — the evaluator model occasionally
// emits the literal string "null" for an optional field instead of omitting it.
public class PostingEvaluatorParsingTests
{
    // A complete, valid tool-use payload with every required field filled — tests override just
    // the field(s) they care about via `overrides`.
    private static IReadOnlyDictionary<string, JsonElement> Input(Dictionary<string, object?> overrides)
    {
        var payload = new Dictionary<string, object?>
        {
            ["company"] = "Acme",
            ["role_title"] = "Software Engineer",
            ["recommendation"] = "good_match",
            ["sponsorship_verdict"] = "pass",
            ["location_match"] = "acceptable",
            ["location_detail"] = "Melbourne hybrid",
            ["experience_match"] = "ideal",
            ["experience_detail"] = "3+ years",
            ["skill_matches"] = Array.Empty<object>(),
            ["salary_assessment"] = "missing",
            ["company_assessment"] = "preferred",
            ["role_type_match"] = "preferred",
            ["orange_flags"] = Array.Empty<string>(),
            ["rationale"] = "Solid match overall.",
        };
        foreach (var (key, value) in overrides) payload[key] = value;

        string json = JsonSerializer.Serialize(payload);
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
    }

    [Fact]
    public void ParseEvaluation_LiteralNullString_DisqualifierHit_NormalizesToNull()
    {
        var input = Input(new() { ["disqualifier_hit"] = "null" });

        var result = PostingEvaluator.ParseEvaluation(input, fallbackSourceUrl: null);

        Assert.Null(result.DisqualifierHit);
    }

    [Fact]
    public void ParseEvaluation_LiteralNullString_SalaryDetail_NormalizesToNull()
    {
        var input = Input(new() { ["salary_detail"] = "null" });

        var result = PostingEvaluator.ParseEvaluation(input, fallbackSourceUrl: null);

        Assert.Null(result.SalaryDetail);
    }

    [Fact]
    public void ParseEvaluation_LiteralNullString_IsCaseInsensitiveAndTrimmed()
    {
        var input = Input(new() { ["salary_detail"] = "  NULL  " });

        var result = PostingEvaluator.ParseEvaluation(input, fallbackSourceUrl: null);

        Assert.Null(result.SalaryDetail);
    }

    [Fact]
    public void ParseEvaluation_RealSalaryDetail_PassesThroughUnchanged()
    {
        var input = Input(new() { ["salary_detail"] = "$120,000 - $140,000 AUD" });

        var result = PostingEvaluator.ParseEvaluation(input, fallbackSourceUrl: null);

        Assert.Equal("$120,000 - $140,000 AUD", result.SalaryDetail);
    }

    [Fact]
    public void ParseEvaluation_MissingOptionalFields_StayNull()
    {
        var input = Input(new());

        var result = PostingEvaluator.ParseEvaluation(input, fallbackSourceUrl: null);

        Assert.Null(result.DisqualifierHit);
        Assert.Null(result.SalaryDetail);
        Assert.Null(result.SponsorshipEvidence);
    }

    [Fact]
    public void ParseEvaluation_SourceUrl_LiteralNullString_FallsBackToNull()
    {
        var input = Input(new() { ["source_url"] = "null" });

        var result = PostingEvaluator.ParseEvaluation(input, fallbackSourceUrl: "https://example.com/job/1");

        // The model's own (bad) "null" string is normalized away, same as the other optional
        // fields — it does not fall back to the caller-supplied URL, since the model explicitly
        // (if badly) answered the field.
        Assert.Null(result.SourceUrl);
    }
}
