using JobSearch.Data;
using PostingEvaluatorBenchmark;
using PostingEvaluatorBenchmark.Fixtures;
using PostingEvaluatorBenchmark.Pricing;
using PostingEvaluatorBenchmark.Providers;
using PostingEvaluatorBenchmark.Scoring;

namespace JobSearchAgent.Tests;

// Structural coverage for the PostingEvaluatorBenchmark harness (cost/accuracy comparison of
// PostingEvaluator across Claude model tiers -- see that project's Program.cs for the runnable
// tool this backs). Everything in this file runs with no ANTHROPIC_API_KEY and makes no network
// calls -- it only checks that the fixtures/ground-truth/pricing/scoring building blocks are
// internally consistent. The one exception, PostingEvaluatorModelComparison_LiveSmokeRun at the
// bottom, is a contract test gated the same way as CvTailorAgentModelEvalTests/
// EmailClassifierEvalTests: no-ops without a key, excluded from CI's default run.
public class PostingEvaluatorBenchmarkTests
{
    private static string? ApiKey => Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

    // ---- Fixture sanity ----

    [Fact]
    public void CriteriaFixtures_AllProfilesRenderNonEmptyYaml()
    {
        Assert.InRange(CriteriaFixtures.All.Count, 15, 25);
        foreach (var profile in CriteriaFixtures.All)
        {
            string yaml = profile.ToJobCriteriaYaml();
            Assert.False(string.IsNullOrWhiteSpace(yaml));
            Assert.Contains("version:", yaml);
            Assert.Contains("profession_category:", yaml);
        }
    }

    [Fact]
    public void PostingFixtures_AllPostingsRenderNonEmptyText()
    {
        Assert.InRange(PostingFixtures.All.Count, 20, 40);
        foreach (var posting in PostingFixtures.All)
        {
            string text = posting.Render();
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.Contains("Company:", text);
            Assert.Contains("Role:", text);
        }
    }

    [Fact]
    public void CriteriaAndPostingIds_AreUnique()
    {
        Assert.Equal(CriteriaFixtures.All.Count, CriteriaFixtures.All.Select(c => c.Id).Distinct().Count());
        Assert.Equal(PostingFixtures.All.Count, PostingFixtures.All.Select(p => p.Id).Distinct().Count());
    }

    // ---- Sampling strategy: size and category coverage ----

    [Fact]
    public void BenchmarkSuite_TotalCaseCount_IsWithinTargetRange()
    {
        // Task brief: "roughly 150-300 total evaluation pairs" -- not the full 20x30 cross product.
        Assert.InRange(BenchmarkSuite.All.Count, 150, 300);
    }

    [Theory]
    [InlineData("citizens_pr_only")]
    [InlineData("sponsorship_not_offered")]
    [InlineData("php_primary")]
    [InlineData("gambling_core")]
    [InlineData("solo_engineer")]
    [InlineData("profession_mismatch")]
    public void BenchmarkSuite_EveryDisqualifierCategory_HitSeveralTimes(string disqualifierId)
    {
        int hits = BenchmarkSuite.All.Count(c => c.Expected.DisqualifierId == disqualifierId);
        Assert.True(hits >= 5, $"Expected at least 5 cases with disqualifier_id={disqualifierId}, found {hits}.");
    }

    [Fact]
    public void BenchmarkSuite_NoDisqualifierCases_AlsoWellRepresented()
    {
        int clean = BenchmarkSuite.All.Count(c => c.Expected.DisqualifierId is null);
        Assert.True(clean >= 30, $"Expected at least 30 non-disqualified cases for dimension-tier coverage, found {clean}.");
    }

    // ---- GroundTruthResolver spot checks (hand-verified against the fixtures' own design intent) ----

    [Fact]
    public void GroundTruth_CitizensPrOnlyPosting_AgainstNonCitizenCriteria_HitsCitizensPrOnly()
    {
        var criteria = Find(CriteriaFixtures.All, "c02_node_au_nonpr_hasvisa");
        var posting = Find(PostingFixtures.All, "p01_citizens_pr_only_au_dotnet");

        var outcome = GroundTruthResolver.Resolve(criteria, posting);

        Assert.Equal("citizens_pr_only", outcome.DisqualifierId);
        Assert.True(outcome.IsDiscardExpected);
    }

    [Fact]
    public void GroundTruth_CitizensPrOnlyPosting_AgainstCitizenCriteria_DoesNotDisqualify()
    {
        var criteria = Find(CriteriaFixtures.All, "c01_dotnet_au_citizen");
        var posting = Find(PostingFixtures.All, "p01_citizens_pr_only_au_dotnet");

        var outcome = GroundTruthResolver.Resolve(criteria, posting);

        Assert.Null(outcome.DisqualifierId);
        Assert.False(outcome.IsDiscardExpected);
    }

    [Fact]
    public void GroundTruth_HasVisaCriteria_SponsorshipNotOfferedPosting_DoesNotTriggerSponsorshipCheck()
    {
        // has_current_work_visa: true -- sponsorship_not_offered must not apply, even though the
        // posting explicitly says "no sponsorship offered".
        var criteria = Find(CriteriaFixtures.All, "c02_node_au_nonpr_hasvisa");
        var posting = Find(PostingFixtures.All, "p02_sponsorship_not_offered_au_node");

        var outcome = GroundTruthResolver.Resolve(criteria, posting);

        Assert.Null(outcome.DisqualifierId);
    }

    [Fact]
    public void GroundTruth_PhpPosting_AgainstDotnetOnlyCriteria_HitsPhpPrimary()
    {
        var criteria = Find(CriteriaFixtures.All, "c01_dotnet_au_citizen");
        var posting = Find(PostingFixtures.All, "p06_php_primary_au");

        var outcome = GroundTruthResolver.Resolve(criteria, posting);

        Assert.Equal("php_primary", outcome.DisqualifierId);
    }

    [Fact]
    public void GroundTruth_PhpPosting_AgainstPhpPreferringCriteria_DoesNotDisqualify()
    {
        var criteria = Find(CriteriaFixtures.All, "c07_php_preferring_au_citizen");
        var posting = Find(PostingFixtures.All, "p06_php_primary_au");

        var outcome = GroundTruthResolver.Resolve(criteria, posting);

        Assert.Null(outcome.DisqualifierId);
    }

    [Fact]
    public void GroundTruth_GamblingPosting_AgainstCriteriaWithoutGamblingDisqualifier_DoesNotDisqualify()
    {
        var criteria = Find(CriteriaFixtures.All, "c16_dotnet_au_citizen_no_gambling_no_solo");
        var posting = Find(PostingFixtures.All, "p11_gambling_core_au_dotnet");

        var outcome = GroundTruthResolver.Resolve(criteria, posting);

        Assert.Null(outcome.DisqualifierId);
    }

    [Fact]
    public void GroundTruth_SoloEngineerPosting_AgainstCriteriaWithoutSoloDisqualifier_DoesNotDisqualify()
    {
        var criteria = Find(CriteriaFixtures.All, "c16_dotnet_au_citizen_no_gambling_no_solo");
        var posting = Find(PostingFixtures.All, "p14_solo_engineer_au_dotnet");

        var outcome = GroundTruthResolver.Resolve(criteria, posting);

        Assert.Null(outcome.DisqualifierId);
    }

    [Fact]
    public void GroundTruth_SoftwarePosting_AgainstNursingCriteria_HitsProfessionMismatch()
    {
        var criteria = Find(CriteriaFixtures.All, "c19_registered_nurse_au_citizen");
        var posting = Find(PostingFixtures.All, "p21_strong_match_au_dotnet");

        var outcome = GroundTruthResolver.Resolve(criteria, posting);

        Assert.Equal("profession_mismatch", outcome.DisqualifierId);
    }

    [Fact]
    public void GroundTruth_StrongMatchPosting_AgainstDesignedCriteria_AllDimensionsPreferredOrIdeal()
    {
        var criteria = Find(CriteriaFixtures.All, "c01_dotnet_au_citizen");
        var posting = Find(PostingFixtures.All, "p21_strong_match_au_dotnet");

        var outcome = GroundTruthResolver.Resolve(criteria, posting);

        Assert.Null(outcome.DisqualifierId);
        Assert.Equal("preferred", outcome.LocationMatch);
        Assert.Equal("ideal", outcome.ExperienceMatch);
        Assert.Equal("target", outcome.SalaryAssessment);
        Assert.Equal("strong", outcome.SkillExpectations.Single(s => s.Dimension == "Backend stack").ExactTierExpected);
    }

    [Fact]
    public void GroundTruth_MissingSalaryPosting_ResolvesToMissing()
    {
        var criteria = Find(CriteriaFixtures.All, "c01_dotnet_au_citizen");
        var posting = Find(PostingFixtures.All, "p24_missing_salary_au");

        var outcome = GroundTruthResolver.Resolve(criteria, posting);

        Assert.Equal("missing", outcome.SalaryAssessment);
    }

    [Fact]
    public void GroundTruth_MissingLocationPosting_ResolvesToMissing()
    {
        var criteria = Find(CriteriaFixtures.All, "c01_dotnet_au_citizen");
        var posting = Find(PostingFixtures.All, "p25_missing_location_au_stack");

        var outcome = GroundTruthResolver.Resolve(criteria, posting);

        Assert.Equal("missing", outcome.LocationMatch);
    }

    // ---- Pricing ----

    [Fact]
    public void ModelPricing_CostUsd_ComputesExpectedAmountForSonnet()
    {
        var usage = new EvaluationUsage("anthropic", "claude-sonnet-5",
            InputTokens: 1000, OutputTokens: 500, CacheReadInputTokens: 2000, CacheCreationInputTokens: 1000);

        decimal cost = ModelPricing.CostUsd(usage);

        // input: 1000 * 3/1e6 = 0.003; cache read: 2000 * 3*0.1/1e6 = 0.0006;
        // cache write: 1000 * 3*1.25/1e6 = 0.00375; output: 500 * 15/1e6 = 0.0075
        decimal expected = 0.003m + 0.0006m + 0.00375m + 0.0075m;
        Assert.Equal(expected, cost);
    }

    [Fact]
    public void ModelPricing_CostUsd_UnknownModel_Throws()
    {
        var usage = new EvaluationUsage("anthropic", "not-a-real-model", 100, 100, 0, 0);
        Assert.Throws<ArgumentException>(() => ModelPricing.CostUsd(usage));
    }

    [Theory]
    [InlineData("claude-sonnet-5")]
    [InlineData("claude-opus-4-8")]
    [InlineData("claude-haiku-4-5")]
    public void ModelPricing_ProductionModelTiers_AreRegistered(string model) =>
        Assert.True(ModelPricing.IsKnownModel(model));

    // ---- Scoring ----

    [Fact]
    public void AccuracyScorer_PerfectMatch_ScoresFullyCorrect()
    {
        var criteria = Find(CriteriaFixtures.All, "c01_dotnet_au_citizen");
        var posting = Find(PostingFixtures.All, "p21_strong_match_au_dotnet");
        var benchmarkCase = new BenchmarkCase(criteria, posting, GroundTruthResolver.Resolve(criteria, posting));

        var actual = new PostingEvaluation
        {
            Company = "Copperfield Technologies",
            RoleTitle = "Software Engineer",
            Recommendation = "strong_match",
            LocationMatch = benchmarkCase.Expected.LocationMatch!,
            LocationDetail = "Melbourne hybrid",
            ExperienceMatch = benchmarkCase.Expected.ExperienceMatch!,
            ExperienceDetail = "3 years",
            SkillMatches = benchmarkCase.Expected.SkillExpectations
                .Select(s => new SkillMatch(s.Dimension, s.ExactTierExpected ?? "missing", "detail"))
                .ToArray(),
            SalaryAssessment = benchmarkCase.Expected.SalaryAssessment!,
            CompanyAssessment = benchmarkCase.Expected.CompanyAssessment!,
            RoleTypeMatch = benchmarkCase.Expected.RoleTypeMatch!,
            Rationale = "Strong match.",
        };

        var score = AccuracyScorer.Score(benchmarkCase, actual);

        Assert.True(score.DisqualifierExactMatch);
        Assert.True(score.DiscardBooleanMatch);
        Assert.Equal(score.DimensionsGraded, score.DimensionsCorrect);
        Assert.True(score.DimensionsGraded > 0);
    }

    [Fact]
    public void AccuracyScorer_MissedDisqualifier_FlagsDiscardBoundaryMismatch()
    {
        var criteria = Find(CriteriaFixtures.All, "c02_node_au_nonpr_hasvisa");
        var posting = Find(PostingFixtures.All, "p01_citizens_pr_only_au_dotnet");
        var benchmarkCase = new BenchmarkCase(criteria, posting, GroundTruthResolver.Resolve(criteria, posting));
        Assert.True(benchmarkCase.Expected.IsDiscardExpected); // sanity on the fixture itself.

        var actual = new PostingEvaluation
        {
            Company = "Fernbank Digital",
            RoleTitle = "Backend Engineer",
            Recommendation = "strong_match", // wrong: should have been "discard".
            LocationMatch = "preferred",
            LocationDetail = "Melbourne hybrid",
            ExperienceMatch = "ideal",
            ExperienceDetail = "3 years",
            SkillMatches = [],
            SalaryAssessment = "target",
            CompanyAssessment = "preferred",
            RoleTypeMatch = "preferred",
            Rationale = "Missed the citizens/PR-only exclusion.",
        };

        var score = AccuracyScorer.Score(benchmarkCase, actual);

        Assert.False(score.DisqualifierExactMatch);
        Assert.False(score.DiscardBooleanMatch);
    }

    // ---- PostingEvaluator's additive model-override constructor ----

    [Fact]
    public void PostingEvaluator_ModelOverrideConstructor_ConstructsWithoutNetworkCall()
    {
        // Constructing only loads the skill file and builds the static tool schema -- no API
        // call happens until EvaluateAsync -- so this verifies the new overload compiles and
        // behaves (doesn't throw) without needing a real key or making a live call.
        var defaultEvaluator = new PostingEvaluator("fake-key");
        var overriddenEvaluator = new PostingEvaluator("fake-key", usageLogger: null, model: "claude-opus-4-8");

        Assert.NotNull(defaultEvaluator);
        Assert.NotNull(overriddenEvaluator);
    }

    private static T Find<T>(IReadOnlyList<T> items, string id) where T : class
    {
        return items.Single(i => (string)(i.GetType().GetProperty("Id")!.GetValue(i) ?? "") == id);
    }

    // ---- Live smoke run (contract test) ----

    // HOW TO RUN (contract test: makes real, billed Claude API calls across up to 3 models --
    // excluded from the default `dotnet test` / CI run, same convention as
    // CvTailorAgentModelEvalTests/EmailClassifierEvalTests):
    //
    //   ANTHROPIC_API_KEY=sk-... dotnet test --filter "FullyQualifiedName~PostingEvaluatorModelComparison_LiveSmokeRun" --logger "console;verbosity=detailed"
    //
    // Runs only a small slice (first 6 cases) against a single model (claude-haiku-4-5, the
    // cheapest tier) as a structural smoke test -- confirms the whole pipeline (fixtures ->
    // ClaudeEvaluationProvider -> real API call -> usage capture -> pricing -> scoring -> report)
    // actually works end to end. The FULL comparison run (~210 cases x 3 models) is the
    // standalone console tool: `dotnet run --project PostingEvaluatorBenchmark` (see that
    // project's Program.cs) -- deliberately not run from this test, to keep `dotnet test
    // --filter Category=contract` itself cheap and fast for anyone who does run the contract
    // suite. NOT executed by this session -- see the PR description for the authorization this
    // is waiting on.
    [Fact]
    [Trait("Category", "contract")]
    public async Task PostingEvaluatorModelComparison_LiveSmokeRun()
    {
        if (ApiKey is null) return;

        var summaries = await BenchmarkRunner.RunAsync(
            ApiKey, models: ["claude-haiku-4-5"], cases: BenchmarkSuite.All.Take(6).ToList(), log: Console.Out);

        Console.WriteLine(BenchmarkReport.Render(summaries));

        Assert.Single(summaries);
        Assert.True(summaries[0].SucceededCases > 0, "Expected at least one case to complete successfully.");
    }
}
