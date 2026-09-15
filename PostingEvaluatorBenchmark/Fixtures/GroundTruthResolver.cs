namespace PostingEvaluatorBenchmark.Fixtures;

// One skill dimension's expected grading outcome. ExactTierExpected is null when the criteria
// uses the current flat `skills:` list format (see CriteriaProfile.UseLegacySkillDimensionsFormat)
// -- evaluate_posting.md itself says the model must use its own domain judgment for tier
// boundaries in that format, so there is no objectively "correct" strong/good/acceptable answer
// to grade against, only whether the posting was addressed at all. IsMissingExpected covers both
// cases uniformly (it's always objectively computable: did this posting supply any keyword for
// this dimension or not).
public record SkillExpectation(string Dimension, string? ExactTierExpected, bool IsMissingExpected);

// The objective, deterministically-computed correct answer for one (CriteriaProfile,
// PostingFixture) pairing. A null field means "not gradable for this pairing" (e.g. every
// non-disqualifier field when the posting is disqualified -- the real evaluator stops scoring
// entirely per evaluate_posting.md Step 1 -- or SalaryAssessment when the posting's and
// criteria's currencies don't match, since comparing e.g. a USD figure against an AUD threshold
// has no well-defined answer). AccuracyScorer.cs skips null fields rather than counting them as
// either correct or incorrect.
public record ExpectedOutcome(
    string? DisqualifierId,
    string? LocationMatch,
    string? ExperienceMatch,
    string? SalaryAssessment,
    string? CompanyAssessment,
    string? RoleTypeMatch,
    IReadOnlyList<SkillExpectation> SkillExpectations)
{
    public bool IsDiscardExpected => DisqualifierId is not null;
}

// Deterministically computes the objectively-correct evaluation outcome for any
// (CriteriaProfile, PostingFixture) pairing, independent of what any model (Claude or otherwise)
// actually returns -- this is the harness's ground-truth oracle, authored once here rather than
// by hand per pairing (see the fixtures' own file headers for why: with ~20 criteria x ~30
// postings sampled into ~150-300 pairs, hand-authoring each pair's expected JSON would be
// unmanageable and error-prone; this function is the single place that logic lives, so every
// pairing -- designed or incidental cross-pairing alike -- gets a correct, consistent answer).
//
// Mirrors evaluate_posting.md's Step 1 (hard disqualifiers, first match wins, stop scoring) and
// Step 2 (dimension scoring, "missing" for anything the posting doesn't address) at the level of
// the deliberately simplified facts these fixtures encode -- it is NOT a reimplementation of the
// production prompt/skill text, just an independent, code-level judge for the same rules applied
// to synthetic data whose "correct" answer was fixed at construction time.
public static class GroundTruthResolver
{
    // Priority order matches evaluate_posting.md Step 1's own ordering: profession/field mismatch
    // is checked as a built-in safety net ahead of the candidate-authored disqualifiers, then
    // sponsorship (citizens/PR before no-sponsorship, per the two-independent-checks ordering the
    // skill documents), then the criteria's own primary-skill exclusion, then gambling, then team
    // size. In this fixture set no posting is designed to hit more than one category against any
    // criteria it's paired with, so the exact tie-break order is a defensive correctness detail,
    // not a live ambiguity.
    public static ExpectedOutcome Resolve(CriteriaProfile c, PostingFixture p)
    {
        if (!string.Equals(c.ProfessionFamily, p.ProfessionFamily, StringComparison.Ordinal))
            return Disqualified("profession_mismatch");

        if (c.Sponsorship is SponsorshipStance.NonCitizenHasVisa or SponsorshipStance.NonCitizenNoVisa)
        {
            if (p.Sponsorship is SponsorshipLine.CitizensOrPrOnly or SponsorshipLine.Both)
                return Disqualified("citizens_pr_only");

            if (c.Sponsorship == SponsorshipStance.NonCitizenNoVisa &&
                p.Sponsorship is SponsorshipLine.NoSponsorshipOffered or SponsorshipLine.Both)
                return Disqualified("sponsorship_not_offered");
        }

        if (c.Skills.Length > 0 && c.Skills[0].Excluded.Length > 0 && Overlaps(p.Dimension0Keywords, c.Skills[0].Excluded))
            return Disqualified(c.PrimaryDisqualifierId);

        if (c.IncludeGamblingDisqualifier && p.IsGamblingIndustry)
            return Disqualified("gambling_core");

        if (c.IncludeSoloEngineerDisqualifier && p.TeamSize == TeamSizeLine.SoloEngineer)
            return Disqualified("solo_engineer");

        return new ExpectedOutcome(
            DisqualifierId: null,
            LocationMatch: ResolveLocation(c, p),
            ExperienceMatch: ResolveExperience(c, p),
            SalaryAssessment: ResolveSalary(c, p),
            CompanyAssessment: ResolveTierLookup(p.CompanyKeywords, c.CompanyPreferred, c.CompanyAcceptable, c.CompanyWeaker, c.CompanyExcluded),
            RoleTypeMatch: ResolveTierLookup(p.RoleTypeKeywords, c.RoleTypePreferred, c.RoleTypeAcceptable, c.RoleTypeWeaker, c.RoleTypeExcluded),
            SkillExpectations: ResolveSkills(c, p));
    }

    private static ExpectedOutcome Disqualified(string id) =>
        new(id, null, null, null, null, null, []);

    private static string ResolveLocation(CriteriaProfile c, PostingFixture p)
    {
        if (p.Country is null && p.Arrangement == ArrangementLine.Unstated) return "missing";
        // Every fixture that states an arrangement also states a country (avoids an otherwise
        // genuinely ambiguous combination) -- see PostingFixtures.cs.
        return string.Equals(p.Country, c.PreferredCountry, StringComparison.OrdinalIgnoreCase) ? "preferred" : "weak";
    }

    private static string ResolveExperience(CriteriaProfile c, PostingFixture p)
    {
        if (p.YearsMidpoint is not double years) return "missing";
        if (years <= c.IdealMaxYears) return "ideal";
        if (years <= c.AcceptableMaxYears) return "acceptable";
        return "excluded";
    }

    // Currency mismatch is intentionally not comparable -- see ExpectedOutcome's doc comment.
    private static string? ResolveSalary(CriteriaProfile c, PostingFixture p)
    {
        if (p.SalaryMidpoint is not decimal amount) return "missing";
        if (!string.Equals(p.SalaryCurrency, c.SalaryCurrency, StringComparison.OrdinalIgnoreCase)) return null;

        if (amount < c.FlagBelow) return "flagged_low";
        if (amount > c.FlagAbove) return "flagged_high";
        if (amount >= c.TargetLow && amount <= c.TargetHigh) return "target";
        return "acceptable";
    }

    private static string ResolveTierLookup(string[] postingKeywords, string[] preferred, string[] acceptable, string[] weaker, string[] excluded)
    {
        if (Overlaps(postingKeywords, preferred)) return "preferred";
        if (Overlaps(postingKeywords, acceptable)) return "acceptable";
        if (Overlaps(postingKeywords, weaker)) return "weaker";
        if (Overlaps(postingKeywords, excluded)) return "excluded";
        return "missing";
    }

    private static List<SkillExpectation> ResolveSkills(CriteriaProfile c, PostingFixture p)
    {
        var keywordSlots = new[] { p.Dimension0Keywords, p.Dimension1Keywords, p.Dimension2Keywords };
        var result = new List<SkillExpectation>(c.Skills.Length);

        for (int i = 0; i < c.Skills.Length; i++)
        {
            var skill = c.Skills[i];
            var keywords = i < keywordSlots.Length ? keywordSlots[i] : [];
            bool isMissing = keywords.Length == 0;

            string? exactTier = null;
            if (c.UseLegacySkillDimensionsFormat)
            {
                exactTier = isMissing ? "missing"
                    : Overlaps(keywords, skill.Strong) ? "strong"
                    : Overlaps(keywords, skill.Good) ? "good"
                    : Overlaps(keywords, skill.Acceptable) ? "acceptable"
                    : Overlaps(keywords, skill.Excluded) ? "excluded"
                    : null; // keyword present but not in any authored tier for THIS criteria -- not gradable exactly, still gradable as "not missing".
            }

            result.Add(new SkillExpectation(skill.Name, exactTier, isMissing));
        }

        return result;
    }

    private static bool Overlaps(string[] postingKeywords, string[] criteriaTerms) =>
        postingKeywords.Any(k => criteriaTerms.Contains(k, StringComparer.OrdinalIgnoreCase));
}
