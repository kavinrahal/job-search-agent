namespace PostingEvaluatorBenchmark.Fixtures;

// One (criteria, posting) pairing plus its objectively-computed expected outcome -- the unit the
// benchmark runner scores each provider against.
public record BenchmarkCase(CriteriaProfile Criteria, PostingFixture Posting, ExpectedOutcome Expected)
{
    public string PairId => $"{Criteria.Id}__{Posting.Id}";
}

// Builds the sampled (criteria, posting) pairs the benchmark actually runs -- NOT the full
// 20x30=600 cross product (the task brief explicitly asks for a representative sample, not an
// exhaustive matrix). Every pair's expected outcome comes from GroundTruthResolver, which is
// valid for ANY (CriteriaProfile, PostingFixture) combination, not just a hand-curated "designed"
// pairing -- so the sampling problem below is purely about which combinations are *interesting*
// to spend API calls on, not about which combinations are gradable (all of them are).
//
// SAMPLING STRATEGY (mirrors how EmailClassifierEvalTests/CvTailorEvalFixtures picked their
// datasets: hand-curated for deliberate coverage, not randomly generated):
//
//   1. Disqualifier-focused postings (p01-p16, each built around one specific hard-disqualifier
//      category) are each paired with a small, hand-picked panel of ~4-6 criteria chosen to
//      include BOTH a "should trigger" case (the criteria's own stance/config makes this
//      disqualifier apply) AND a "should NOT trigger" case (a different stance/config where the
//      same posting must NOT be disqualified) -- this is what lets accuracy scoring catch false
//      positives, not just false negatives, on the safety-critical disqualifier dimension.
//   2. Non-software-profession postings (p17-p20) are paired with their own matching-profession
//      criteria (clean-match / same-family case) plus 2-3 software criteria (profession_mismatch
//      case) -- profession_mismatch is a built-in safety net the model applies independent of
//      anything the candidate authored, so it needs its own explicit true-positive coverage.
//   3. Clean/spectrum postings (p21-p30, covering strong_match through weak/discard-adjacent
//      cases and the ambiguous-salary/unstated-location/borderline-experience edge cases) are
//      each paired with a broader ~8-11 criteria panel spanning different salary currencies,
//      experience ranges, legacy vs. flat skill-list format, and the no-gambling/no-solo profile
//      -- this is where most of the per-dimension (location/experience/salary/company/role-type/
//      skill-tier) accuracy signal comes from, so it gets the most pairs per posting.
//
// Total: ~205 pairs (see JobSearchAgent.Tests/PostingEvaluatorBenchmarkTests.cs for the exact
// count assertion and per-category minimums) --
// within the ~150-300 range the task brief asked for, with every disqualifier category (including
// "no disqualifier") hit well over a dozen times.
public static class BenchmarkSuite
{
    private static readonly Dictionary<string, CriteriaProfile> CriteriaById =
        CriteriaFixtures.All.ToDictionary(c => c.Id);
    private static readonly Dictionary<string, PostingFixture> PostingById =
        PostingFixtures.All.ToDictionary(p => p.Id);

    // ---- Named panels (criteria IDs), reused across postings in the same category ----

    private static readonly string[] PanelCitizensPrOnly =
        ["c01_dotnet_au_citizen", "c05_ruby_uk_unanswered", "c02_node_au_nonpr_hasvisa", "c03_python_au_nonpr_novisa", "c08_dotnet_au_nonpr_hasvisa_senior", "c14_node_au_flatlist_nonpr_novisa"];

    private static readonly string[] PanelSponsorshipNotOffered =
        ["c01_dotnet_au_citizen", "c02_node_au_nonpr_hasvisa", "c03_python_au_nonpr_novisa", "c10_node_us_nonpr_novisa", "c14_node_au_flatlist_nonpr_novisa", "c06_go_au_nonpr_novisa"];

    private static readonly string[] PanelBoth =
        ["c01_dotnet_au_citizen", "c02_node_au_nonpr_hasvisa", "c03_python_au_nonpr_novisa", "c05_ruby_uk_unanswered", "c14_node_au_flatlist_nonpr_novisa", "c10_node_us_nonpr_novisa"];

    private static readonly string[] PanelCitizensPrOnlyUs =
        ["c04_java_us_citizen_senior", "c15_dotnet_us_flatlist_remote", "c02_node_au_nonpr_hasvisa", "c10_node_us_nonpr_novisa", "c08_dotnet_au_nonpr_hasvisa_senior", "c03_python_au_nonpr_novisa"];

    private static readonly string[] PanelSponsorshipNotOfferedUs =
        ["c04_java_us_citizen_senior", "c02_node_au_nonpr_hasvisa", "c10_node_us_nonpr_novisa", "c03_python_au_nonpr_novisa", "c14_node_au_flatlist_nonpr_novisa", "c06_go_au_nonpr_novisa"];

    private static readonly string[] PanelPhpPrimary =
        ["c01_dotnet_au_citizen", "c02_node_au_nonpr_hasvisa", "c03_python_au_nonpr_novisa", "c04_java_us_citizen_senior", "c07_php_preferring_au_citizen", "c13_dotnet_au_flatlist"];

    private static readonly string[] PanelPhpNegativeControl =
        ["c07_php_preferring_au_citizen", "c01_dotnet_au_citizen", "c02_node_au_nonpr_hasvisa", "c13_dotnet_au_flatlist"];

    // Shared by both the gambling_core and solo_engineer postings below (same criteria selection
    // logic applies to both categories, so one stack-matched panel set serves both) --
    // deliberately stack-matched to each posting's own backend (Dimension0Keywords): pairing e.g.
    // a Python posting against a C#/.NET-preferring criteria would hit that criteria's OWN
    // primary-skill exclusion (most profiles exclude most other stacks, mirroring the real
    // backend_not_dotnet disqualifier) before the resolver ever reaches the gambling/solo check --
    // still a correct ground-truth answer, but it would silently steal samples from the category
    // each panel is meant to exercise. c16 (no gambling_core/solo_engineer authored) is the shared
    // negative control for both categories.
    private static readonly string[] StackMatchedPanelDotnet =
        ["c01_dotnet_au_citizen", "c08_dotnet_au_nonpr_hasvisa_senior", "c09_dotnet_au_citizen_junior", "c13_dotnet_au_flatlist", "c17_dotnet_au_citizen_3dim_testing", "c16_dotnet_au_citizen_no_gambling_no_solo"];
    private static readonly string[] StackMatchedPanelNode =
        ["c02_node_au_nonpr_hasvisa", "c10_node_us_nonpr_novisa", "c14_node_au_flatlist_nonpr_novisa"];
    private static readonly string[] StackMatchedPanelPython =
        ["c03_python_au_nonpr_novisa", "c12_python_au_citizen_3dim_cloud"];

    private static readonly string[] BroadSpectrumPanel =
        ["c01_dotnet_au_citizen", "c02_node_au_nonpr_hasvisa", "c03_python_au_nonpr_novisa", "c04_java_us_citizen_senior", "c06_go_au_nonpr_novisa", "c08_dotnet_au_nonpr_hasvisa_senior", "c09_dotnet_au_citizen_junior", "c12_python_au_citizen_3dim_cloud", "c13_dotnet_au_flatlist", "c16_dotnet_au_citizen_no_gambling_no_solo", "c17_dotnet_au_citizen_3dim_testing"];

    public static readonly IReadOnlyList<BenchmarkCase> All = BuildAll();

    private static IReadOnlyList<BenchmarkCase> BuildAll()
    {
        var pairs = new List<(string PostingId, string[] Panel)>
        {
            ("p01_citizens_pr_only_au_dotnet", PanelCitizensPrOnly),
            ("p02_sponsorship_not_offered_au_node", PanelSponsorshipNotOffered),
            ("p03_both_sponsorship_checks_au_python", PanelBoth),
            ("p04_citizens_pr_only_us_java", PanelCitizensPrOnlyUs),
            ("p05_sponsorship_not_offered_us_node", PanelSponsorshipNotOfferedUs),

            ("p06_php_primary_au", PanelPhpPrimary),
            ("p07_php_primary_us", PanelPhpPrimary),
            ("p08_php_symfony_au", PanelPhpPrimary),
            ("p09_php_laravel_remote_au", PanelPhpPrimary),
            ("p10_php_primary_negative_control_au", PanelPhpNegativeControl),

            ("p11_gambling_core_au_dotnet", StackMatchedPanelDotnet),
            ("p12_gambling_core_us_node", StackMatchedPanelNode),
            ("p13_gambling_core_au_python", StackMatchedPanelPython),

            ("p14_solo_engineer_au_dotnet", StackMatchedPanelDotnet),
            ("p15_solo_engineer_us_python", StackMatchedPanelPython),
            ("p16_solo_engineer_au_node", StackMatchedPanelNode),

            ("p21_strong_match_au_dotnet", BroadSpectrumPanel),
            ("p22_acceptable_dimensions_flagged_low_salary", BroadSpectrumPanel),
            ("p23_excluded_experience_range", BroadSpectrumPanel),
            ("p24_missing_salary_au", BroadSpectrumPanel),
            ("p25_missing_location_au_stack", BroadSpectrumPanel),
            ("p26_borderline_experience_midpoint", BroadSpectrumPanel),
            ("p27_flagged_high_salary_au", BroadSpectrumPanel),
            ("p30_missing_frontend_dimension", BroadSpectrumPanel),

            ("p28_clean_match_java_us_senior", Prepend("c04_java_us_citizen_senior", BroadSpectrumPanel)),
            ("p29_clean_match_go_au", Prepend("c06_go_au_nonpr_novisa", Prepend("c12_python_au_citizen_3dim_cloud", BroadSpectrumPanel))),

            ("p17_business_analyst_clean_au", ["c18_business_analyst_au_citizen", "c01_dotnet_au_citizen", "c19_registered_nurse_au_citizen", "c20_secondary_teacher_au_nonpr_novisa"]),
            ("p18_registered_nurse_clean_au", ["c19_registered_nurse_au_citizen", "c01_dotnet_au_citizen", "c18_business_analyst_au_citizen", "c20_secondary_teacher_au_nonpr_novisa"]),
            ("p19_teacher_clean_au", ["c20_secondary_teacher_au_nonpr_novisa", "c01_dotnet_au_citizen", "c18_business_analyst_au_citizen", "c19_registered_nurse_au_citizen"]),
            ("p20_teacher_sponsorship_not_offered_au", ["c20_secondary_teacher_au_nonpr_novisa", "c18_business_analyst_au_citizen", "c01_dotnet_au_citizen"]),
        };

        var cases = new List<BenchmarkCase>();
        foreach (var (postingId, panel) in pairs)
        {
            var posting = PostingById[postingId];
            foreach (var criteriaId in panel.Distinct())
            {
                var criteria = CriteriaById[criteriaId];
                cases.Add(new BenchmarkCase(criteria, posting, GroundTruthResolver.Resolve(criteria, posting)));
            }
        }
        return cases;
    }

    private static string[] Prepend(string id, string[] panel) => [id, .. panel];
}
