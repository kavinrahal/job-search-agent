using System.Text.Json;
using JobSearch.Data;

namespace JobSearchAgent.Tests;

// Exercises PostingPreFilterAgent.ParseResult directly against a fabricated tool-use input, the
// same structural-contract discipline as PostingEvaluatorParsingTests — deserializes correctly,
// required fields present, enum values valid, and the literal-"null"-string normalization holds
// for this agent's optional fields too (same convention as PostingEvaluator.ParseEvaluation).
public class PostingPreFilterAgentParsingTests
{
    private static IReadOnlyDictionary<string, JsonElement> Input(Dictionary<string, object?> overrides)
    {
        var payload = new Dictionary<string, object?>();
        foreach (var (key, value) in overrides) payload[key] = value;

        string json = JsonSerializer.Serialize(payload);
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
    }

    [Fact]
    public void ParseResult_NoFieldsPresent_BothNull()
    {
        var input = Input(new());

        var result = PostingPreFilterAgent.ParseResult(input);

        Assert.Null(result.DisqualifierHit);
        Assert.Null(result.Evidence);
    }

    [Theory]
    [InlineData("sponsorship")]
    [InlineData("php_primary")]
    [InlineData("gambling")]
    [InlineData("solo_engineer")]
    public void ParseResult_EachValidDisqualifierId_ParsesThrough(string id)
    {
        var input = Input(new()
        {
            ["disqualifier_hit"] = id,
            ["evidence"] = "exact quoted phrase from the posting",
        });

        var result = PostingPreFilterAgent.ParseResult(input);

        Assert.Equal(id, result.DisqualifierHit);
        Assert.Equal("exact quoted phrase from the posting", result.Evidence);
    }

    [Fact]
    public void ParseResult_LiteralNullString_DisqualifierHit_NormalizesToNull()
    {
        var input = Input(new() { ["disqualifier_hit"] = "null" });

        var result = PostingPreFilterAgent.ParseResult(input);

        Assert.Null(result.DisqualifierHit);
    }

    [Fact]
    public void ParseResult_LiteralNullString_Evidence_NormalizesToNull()
    {
        var input = Input(new() { ["evidence"] = "  NULL  " });

        var result = PostingPreFilterAgent.ParseResult(input);

        Assert.Null(result.Evidence);
    }

    [Fact]
    public void ParseResult_DisqualifierHitWithoutEvidence_StillParses()
    {
        // The schema doesn't hard-enforce "evidence required when disqualifier_hit is set" (see
        // PostingEvaluator's disqualifier_hit/sponsorship_evidence, same convention) — this pins
        // down that a model slip here doesn't throw, it just leaves Evidence null.
        var input = Input(new() { ["disqualifier_hit"] = "gambling" });

        var result = PostingPreFilterAgent.ParseResult(input);

        Assert.Equal("gambling", result.DisqualifierHit);
        Assert.Null(result.Evidence);
    }

    // The tool schema's `enum` constraint on disqualifier_hit is a strong hint to the model, not
    // a hard guarantee — a live eval run against real production data caught the model emitting
    // "senior_role" (not one of the four scoped ids) despite the schema. A narrow prefilter that
    // starts inventing its own categories defeats the entire point, so this must be rejected at
    // the parsing boundary, not trusted through.
    [Fact]
    public void ParseResult_OutOfScopeDisqualifierId_NormalizesToNull()
    {
        var input = Input(new() { ["disqualifier_hit"] = "senior_role", ["evidence"] = "Senior Engineer" });

        var result = PostingPreFilterAgent.ParseResult(input);

        Assert.Null(result.DisqualifierHit);
    }

    // Evidence for a rejected out-of-scope disqualifier is meaningless on its own — it must be
    // dropped along with the invalid DisqualifierHit, not left dangling.
    [Fact]
    public void ParseResult_OutOfScopeDisqualifierId_AlsoDropsEvidence()
    {
        var input = Input(new() { ["disqualifier_hit"] = "role_type_mismatch", ["evidence"] = "not an engineering role" });

        var result = PostingPreFilterAgent.ParseResult(input);

        Assert.Null(result.DisqualifierHit);
        Assert.Null(result.Evidence);
    }

    [Fact]
    public void ParseResult_OutOfScopeDisqualifierId_IsCaseInsensitive()
    {
        var input = Input(new() { ["disqualifier_hit"] = "SPONSORSHIP" });

        var result = PostingPreFilterAgent.ParseResult(input);

        // Valid id, just differently-cased — should still pass through, not be rejected.
        Assert.Equal("SPONSORSHIP", result.DisqualifierHit);
    }

    // =========================================================================
    // RejectUnsupportedPhpPrimary — deterministic code-level guard on top of the skill file's
    // own "mandatory literal-text gate". A live eval against real production postings (see the
    // eval suite referenced from the PR) showed claude-haiku-4-5 repeatedly misusing
    // php_primary as a stand-in for "backend isn't the candidate's preferred language" — flagging
    // Java/Python roles as php_primary — despite multiple rounds of explicit prompt guardrails.
    // This closes the gap in code instead of relying on the model to follow the prompt.
    // =========================================================================

    [Fact]
    public void RejectUnsupportedPhpPrimary_PostingMentionsPhp_KeepsTheHit()
    {
        var result = new PostingPreFilterResult { DisqualifierHit = "php_primary", Evidence = "Must have 5+ years PHP experience" };

        var final = PostingPreFilterAgent.RejectUnsupportedPhpPrimary(result, "We're looking for a senior PHP developer to join our team.");

        Assert.Equal("php_primary", final.DisqualifierHit);
        Assert.Equal("Must have 5+ years PHP experience", final.Evidence);
    }

    [Theory]
    [InlineData("Experience with Laravel required.")]
    [InlineData("Our stack is built on Symfony.")]
    [InlineData("WordPress plugin development experience a must.")]
    public void RejectUnsupportedPhpPrimary_PostingMentionsPhpFramework_KeepsTheHit(string postingText)
    {
        var result = new PostingPreFilterResult { DisqualifierHit = "php_primary", Evidence = "framework mention" };

        var final = PostingPreFilterAgent.RejectUnsupportedPhpPrimary(result, postingText);

        Assert.Equal("php_primary", final.DisqualifierHit);
    }

    // The exact real-world failure case this guard exists for: the model flags php_primary with
    // reasoning about Java/Python/etc. not being C#/.NET, but the word "PHP" never appears.
    [Fact]
    public void RejectUnsupportedPhpPrimary_PostingNeverMentionsPhp_DropsTheHit()
    {
        var result = new PostingPreFilterResult
        {
            DisqualifierHit = "php_primary",
            Evidence = "The job title is \"Core Java Developer\" — Java is the primary backend language, not C#/.NET",
        };

        var final = PostingPreFilterAgent.RejectUnsupportedPhpPrimary(result, "Core Java Developer — build backend services in Java and Spring Boot.");

        Assert.Null(final.DisqualifierHit);
        Assert.Null(final.Evidence);
    }

    [Fact]
    public void RejectUnsupportedPhpPrimary_NoDisqualifierHit_PassesThroughUnchanged()
    {
        var result = new PostingPreFilterResult();

        var final = PostingPreFilterAgent.RejectUnsupportedPhpPrimary(result, "Java backend role.");

        Assert.Null(final.DisqualifierHit);
    }

    [Fact]
    public void RejectUnsupportedPhpPrimary_OtherDisqualifierHit_NeverInspected()
    {
        // Only php_primary gets the literal-text guard — sponsorship/gambling/solo_engineer must
        // pass through untouched regardless of what the posting text contains.
        var result = new PostingPreFilterResult { DisqualifierHit = "gambling", Evidence = "online casino operator" };

        var final = PostingPreFilterAgent.RejectUnsupportedPhpPrimary(result, "No PHP or gambling terms in this posting at all.");

        Assert.Equal("gambling", final.DisqualifierHit);
    }

    // =========================================================================
    // RejectSponsorshipForCitizenOrPr — deterministic code-level guard, same rationale as
    // RejectUnsupportedPhpPrimary above: a live eval showed claude-haiku-4-5 still flagging
    // `sponsorship` even when the candidate's own criteria explicitly states
    // citizen_or_permanent_resident: true, despite the skill file's explicit instruction that
    // this check never applies to a citizen/PR candidate. Closed deterministically in code.
    // =========================================================================

    [Fact]
    public void RejectSponsorshipForCitizenOrPr_CriteriaStatesTrue_DropsTheHit()
    {
        var result = new PostingPreFilterResult { DisqualifierHit = "sponsorship", Evidence = "No visa sponsorship offered" };
        const string criteria = "sponsorship:\n  candidate_status:\n    citizen_or_permanent_resident: true\n    has_current_work_visa: false\n";

        var final = PostingPreFilterAgent.RejectSponsorshipForCitizenOrPr(result, criteria);

        Assert.Null(final.DisqualifierHit);
        Assert.Null(final.Evidence);
    }

    [Fact]
    public void RejectSponsorshipForCitizenOrPr_CriteriaStatesFalse_KeepsTheHit()
    {
        var result = new PostingPreFilterResult { DisqualifierHit = "sponsorship", Evidence = "No visa sponsorship offered" };
        const string criteria = "sponsorship:\n  candidate_status:\n    citizen_or_permanent_resident: false\n    has_current_work_visa: false\n";

        var final = PostingPreFilterAgent.RejectSponsorshipForCitizenOrPr(result, criteria);

        Assert.Equal("sponsorship", final.DisqualifierHit);
    }

    [Fact]
    public void RejectSponsorshipForCitizenOrPr_CriteriaSilentOnStatus_KeepsTheHit()
    {
        // Silence isn't the same as an explicit "true" — the guard only fires on an unambiguous
        // affirmative statement, matching the field's own "omitted = unanswered" convention.
        var result = new PostingPreFilterResult { DisqualifierHit = "sponsorship", Evidence = "No visa sponsorship offered" };

        var final = PostingPreFilterAgent.RejectSponsorshipForCitizenOrPr(result, "target_job_titles: [Software Engineer]");

        Assert.Equal("sponsorship", final.DisqualifierHit);
    }

    [Fact]
    public void RejectSponsorshipForCitizenOrPr_OtherDisqualifierHit_NeverInspected()
    {
        var result = new PostingPreFilterResult { DisqualifierHit = "gambling", Evidence = "online casino" };
        const string criteria = "citizen_or_permanent_resident: true";

        var final = PostingPreFilterAgent.RejectSponsorshipForCitizenOrPr(result, criteria);

        Assert.Equal("gambling", final.DisqualifierHit);
    }
}
