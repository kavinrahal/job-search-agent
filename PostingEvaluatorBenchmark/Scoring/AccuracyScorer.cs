using JobSearch.Data;
using PostingEvaluatorBenchmark.Fixtures;

namespace PostingEvaluatorBenchmark.Scoring;

// Per-case grading result. Every *Graded count is a denominator that already excludes fields
// ExpectedOutcome marked as not gradable for this pairing (see that type's doc comment) -- a
// field never counted in the denominator was never counted against the model either.
public record CaseScore(
    string PairId,
    bool DisqualifierExactMatch,          // did disqualifier_hit (or its absence) match exactly, including the specific id?
    bool DiscardBooleanMatch,              // coarser, always-gradable signal: did recommendation=="discard" agree with IsDiscardExpected?
    int DimensionsCorrect,
    int DimensionsGraded,
    IReadOnlyList<string> Mismatches);

// Grades one real PostingEvaluation (from any IEvaluationProvider) against a BenchmarkCase's
// objectively-computed ExpectedOutcome. This is intentionally NOT a prose-quality check (see
// evaluate_posting.md's own "rationale" field, which is never graded here) -- only the
// enumerated, ground-truth-backed fields evaluate_posting.md's schema defines.
//
// recommendation's fine-grained tier (strong_match vs. good_match vs. weak_match) is deliberately
// NOT graded exactly -- evaluate_posting.md's own Step 4 default thresholds are inherently a
// judgment call ("no significant orange flags", "minor orange flags only") that this harness's
// synthetic fixtures don't attempt to pin down to the tier. What IS graded, and graded strictly,
// is the discard/non-discard boundary (DiscardBooleanMatch) -- that boundary is fully determined
// by Step 1's hard disqualifiers, which ARE objectively computable (see GroundTruthResolver), and
// it is also the safety-critical one: a missed disqualifier means a posting the candidate
// explicitly asked to never see (visa exclusion, gambling industry, etc.) gets surfaced anyway.
public static class AccuracyScorer
{
    public static CaseScore Score(BenchmarkCase benchmarkCase, PostingEvaluation actual)
    {
        var expected = benchmarkCase.Expected;
        var mismatches = new List<string>();

        string? actualDisqualifier = string.IsNullOrWhiteSpace(actual.DisqualifierHit) ? null : actual.DisqualifierHit;
        bool disqualifierExact = string.Equals(actualDisqualifier, expected.DisqualifierId, StringComparison.OrdinalIgnoreCase);
        if (!disqualifierExact)
            mismatches.Add($"disqualifier_hit: expected={expected.DisqualifierId ?? "(none)"} actual={actualDisqualifier ?? "(none)"}");

        bool actualIsDiscard = string.Equals(actual.Recommendation, "discard", StringComparison.OrdinalIgnoreCase);
        bool discardMatch = actualIsDiscard == expected.IsDiscardExpected;
        if (!discardMatch)
            mismatches.Add($"discard boundary: expected_discard={expected.IsDiscardExpected} actual_recommendation={actual.Recommendation}");

        int graded = 0, correct = 0;

        // When the posting is expected to be disqualified, evaluate_posting.md's own Step 1
        // instructs the model to stop scoring entirely -- there's nothing meaningful to grade on
        // the remaining dimensions either way, so none of them count toward the denominator.
        if (!expected.IsDiscardExpected)
        {
            GradeField("location_match", expected.LocationMatch, actual.LocationMatch, mismatches, ref graded, ref correct);
            GradeField("experience_match", expected.ExperienceMatch, actual.ExperienceMatch, mismatches, ref graded, ref correct);
            GradeField("salary_assessment", expected.SalaryAssessment, actual.SalaryAssessment, mismatches, ref graded, ref correct);
            GradeField("company_assessment", expected.CompanyAssessment, actual.CompanyAssessment, mismatches, ref graded, ref correct);
            GradeField("role_type_match", expected.RoleTypeMatch, actual.RoleTypeMatch, mismatches, ref graded, ref correct);

            foreach (var skillExpectation in expected.SkillExpectations)
                GradeSkill(skillExpectation, actual.SkillMatches, mismatches, ref graded, ref correct);
        }

        return new CaseScore(benchmarkCase.PairId, disqualifierExact, discardMatch, correct, graded, mismatches);
    }

    private static void GradeField(string label, string? expected, string? actual, List<string> mismatches, ref int graded, ref int correct)
    {
        if (expected is null) return; // not comparable for this pairing (see ExpectedOutcome doc comment).
        graded++;
        if (string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            correct++;
        else
            mismatches.Add($"{label}: expected={expected} actual={actual ?? "(null)"}");
    }

    private static void GradeSkill(SkillExpectation expectation, SkillMatch[] actualSkills, List<string> mismatches, ref int graded, ref int correct)
    {
        var actualMatch = actualSkills.FirstOrDefault(s => string.Equals(s.Dimension, expectation.Dimension, StringComparison.OrdinalIgnoreCase));

        if (expectation.IsMissingExpected)
        {
            graded++;
            string? actualTier = actualMatch?.Match;
            if (string.Equals(actualTier, "missing", StringComparison.OrdinalIgnoreCase)) correct++;
            else mismatches.Add($"skill[{expectation.Dimension}]: expected=missing actual={actualTier ?? "(absent)"}");
            return;
        }

        if (expectation.ExactTierExpected is not null)
        {
            graded++;
            string? actualTier = actualMatch?.Match;
            if (string.Equals(actualTier, expectation.ExactTierExpected, StringComparison.OrdinalIgnoreCase)) correct++;
            else mismatches.Add($"skill[{expectation.Dimension}]: expected={expectation.ExactTierExpected} actual={actualTier ?? "(absent)"}");
            return;
        }

        // Flat skills: list format with a keyword that doesn't land in any of this criteria's own
        // tier lists, or (per CriteriaProfile.UseLegacySkillDimensionsFormat) a profile where the
        // model must use its own domain judgment -- only "was this dimension addressed at all"
        // is objectively gradable here (see GroundTruthResolver.ResolveSkills / SkillExpectation).
        graded++;
        string? tier = actualMatch?.Match;
        bool addressed = tier is not null && !string.Equals(tier, "missing", StringComparison.OrdinalIgnoreCase);
        if (addressed) correct++;
        else mismatches.Add($"skill[{expectation.Dimension}]: expected=(present, exact tier not gradable) actual={tier ?? "(absent)"}");
    }
}
