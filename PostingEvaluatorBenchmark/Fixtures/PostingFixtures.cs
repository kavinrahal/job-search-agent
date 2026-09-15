namespace PostingEvaluatorBenchmark.Fixtures;

// 30 synthetic job postings (no real data), each with every fact GroundTruthResolver needs baked
// in at construction time -- see that class for how a posting's structured facts combine with a
// CriteriaProfile's structured facts into an objective expected outcome for any pairing. Grouped
// below by what each posting was built to exercise; BenchmarkSuite.cs decides which criteria each
// one is actually sampled against (both "should trigger" and "should NOT trigger" pairings, per
// category, to check for both false negatives and false positives).
public static class PostingFixtures
{
    // Shared keyword vocab matching CriteriaFixtures' lists exactly, so postings can opt into a
    // specific company/role-type tier deterministically. Declared before All below -- C# runs
    // static field initializers in textual declaration order, so these must come first.
    private static readonly string[] ProductCompany = ["Product company with paying customers", "Series B scale-up"];
    private static readonly string[] StartupOrEnterprise = ["Early-stage startup with product-market fit"];
    private static readonly string[] ProductEngineering = ["Product engineering, owning features end-to-end"];
    private static readonly string[] PlatformOrBackend = ["Backend-focused with some frontend"];

    public static readonly IReadOnlyList<PostingFixture> All =
    [
        // ---- citizens_pr_only / sponsorship_not_offered (5) ----
        new("p01_citizens_pr_only_au_dotnet", "Explicit citizens/PR-only language, AU, C#/.NET.",
            "Fernbank Digital", "Backend Engineer", "software", SponsorshipLine.CitizensOrPrOnly,
            "Australia", ArrangementLine.Hybrid, "Melbourne, VIC", 3, "3+ years", 105_000, "AUD", "$95,000 - $115,000 AUD",
            false, TeamSizeLine.SmallTeam, ["C#", ".NET", "ASP.NET Core"], ["React"], [],
            ProductCompany, ProductEngineering, "Build merchant-facing APIs for our payments platform.", ""),

        new("p02_sponsorship_not_offered_au_node", "Explicit no-sponsorship language, AU, Node/TS.",
            "Coastline Retail", "Full Stack Developer", "software", SponsorshipLine.NoSponsorshipOffered,
            "Australia", ArrangementLine.Remote, "Sydney, NSW", 3, "3-5 years", 100_000, "AUD", "$90,000 - $110,000 AUD",
            false, TeamSizeLine.SmallTeam, ["Node.js", "TypeScript"], ["React"], [],
            ProductCompany, ProductEngineering, "Own the checkout and inventory services end to end.", ""),

        new("p03_both_sponsorship_checks_au_python", "Both citizens/PR-only AND no-sponsorship language, AU, Python.",
            "Harbourline Analytics", "Backend Engineer", "software", SponsorshipLine.Both,
            "Australia", ArrangementLine.Hybrid, "Brisbane, QLD", 5, "5+ years", 125_000, "AUD", "$110,000 - $140,000 AUD",
            false, TeamSizeLine.SmallTeam, ["Python", "Django"], ["Vue.js"], [],
            ProductCompany, ProductEngineering, "Build data pipelines for our analytics product.", ""),

        new("p04_citizens_pr_only_us_java", "Explicit citizens/PR-only language, United States, Java.",
            "Ridgeline Systems", "Staff Engineer", "software", SponsorshipLine.CitizensOrPrOnly,
            "United States", ArrangementLine.Hybrid, "Austin, TX", 7, "7+ years", 150_000, "USD", "$130,000 - $170,000 USD",
            false, TeamSizeLine.LargeTeam, ["Java", "Spring Boot"], ["Angular"], [],
            ProductCompany, ProductEngineering, "Lead architecture for our core platform services.", ""),

        new("p05_sponsorship_not_offered_us_node", "Explicit no-sponsorship language, United States, Node/TS.",
            "Beacon Software", "Software Engineer", "software", SponsorshipLine.NoSponsorshipOffered,
            "United States", ArrangementLine.Remote, "Remote (US)", 4, "4-6 years", 115_000, "USD", "$100,000 - $130,000 USD",
            false, TeamSizeLine.SmallTeam, ["Node.js", "TypeScript"], ["React"], [],
            ProductCompany, ProductEngineering, "Build APIs for our subscription billing platform.", ""),

        // ---- php_primary (backend-stack exclusion) -- 4 that should trigger it, 1 that should NOT ----
        new("p06_php_primary_au", "PHP/Laravel-primary backend, AU -- should trigger php_primary against any profile that excludes PHP.",
            "Bramblewood Media", "Backend Developer", "software", SponsorshipLine.None,
            "Australia", ArrangementLine.Hybrid, "Melbourne, VIC", 3, "3+ years", 95_000, "AUD", "$85,000 - $105,000 AUD",
            false, TeamSizeLine.SmallTeam, ["PHP", "Laravel"], ["Vue.js"], [],
            ProductCompany, ProductEngineering, "Maintain and extend our Laravel-based CMS platform.", ""),

        new("p07_php_primary_us", "PHP/Laravel-primary backend, United States.",
            "Fenwick Commerce", "Software Engineer", "software", SponsorshipLine.None,
            "United States", ArrangementLine.Remote, "Remote (US)", 4, "4+ years", 110_000, "USD", "$95,000 - $125,000 USD",
            false, TeamSizeLine.SmallTeam, ["PHP", "Laravel", "MySQL"], ["React"], [],
            ProductCompany, ProductEngineering, "Own our PHP monolith's checkout module.", ""),

        new("p08_php_symfony_au", "PHP/Symfony-primary backend, AU.",
            "Alderney Systems", "Backend Engineer", "software", SponsorshipLine.None,
            "Australia", ArrangementLine.Hybrid, "Perth, WA", 2, "2-4 years", 90_000, "AUD", "$80,000 - $100,000 AUD",
            false, TeamSizeLine.SmallTeam, ["PHP", "Symfony"], ["Vue.js"], [],
            ProductCompany, ProductEngineering, "Build internal tools on our Symfony platform.", ""),

        new("p09_php_laravel_remote_au", "PHP/Laravel-primary backend, AU remote.",
            "Northwood Labs", "Full Stack Developer", "software", SponsorshipLine.None,
            "Australia", ArrangementLine.Remote, "Remote (AU)", 3, "3-5 years", 100_000, "AUD", "$90,000 - $110,000 AUD",
            false, TeamSizeLine.SmallTeam, ["PHP", "Laravel"], ["React"], [],
            ProductCompany, ProductEngineering, "Build customer-facing features on our Laravel app.", ""),

        new("p10_php_primary_negative_control_au", "PHP/Laravel-primary backend -- for c07 (PHP-preferring criteria) this should NOT disqualify.",
            "Larchmont Digital", "Backend Developer", "software", SponsorshipLine.None,
            "Australia", ArrangementLine.Hybrid, "Adelaide, SA", 3, "3+ years", 105_000, "AUD", "$95,000 - $115,000 AUD",
            false, TeamSizeLine.SmallTeam, ["PHP", "Laravel", "Symfony"], ["Vue.js"], [],
            ProductCompany, ProductEngineering, "Own our Laravel-based marketplace backend.", ""),

        // ---- gambling_core (3) ----
        new("p11_gambling_core_au_dotnet", "Online casino / sports betting, AU, otherwise a strong C#/.NET match.",
            "Silverbet Entertainment", "Backend Engineer", "software", SponsorshipLine.None,
            "Australia", ArrangementLine.Hybrid, "Melbourne, VIC", 4, "4+ years", 130_000, "AUD", "$120,000 - $145,000 AUD",
            true, TeamSizeLine.LargeTeam, ["C#", ".NET", "ASP.NET Core"], ["React"], [],
            ProductCompany, ProductEngineering, "Build real-time odds services for our online casino and sportsbook platform.", ""),

        new("p12_gambling_core_us_node", "iGaming/wagering platform, United States, Node/TS.",
            "Redline Wagering", "Software Engineer", "software", SponsorshipLine.None,
            "United States", ArrangementLine.Remote, "Remote (US)", 4, "4+ years", 125_000, "USD", "$110,000 - $140,000 USD",
            true, TeamSizeLine.LargeTeam, ["Node.js", "TypeScript"], ["React"], [],
            ProductCompany, ProductEngineering, "Join our iGaming platform team building the wagering engine.", ""),

        new("p13_gambling_core_au_python", "Sportsbook platform, AU, Python.",
            "Ironclad Sportsbook", "Backend Developer", "software", SponsorshipLine.None,
            "Australia", ArrangementLine.Hybrid, "Sydney, NSW", 5, "5+ years", 135_000, "AUD", "$120,000 - $150,000 AUD",
            true, TeamSizeLine.LargeTeam, ["Python", "Django"], ["Vue.js"], [],
            ProductCompany, ProductEngineering, "Build the pricing engine for our sportsbook product.", ""),

        // ---- solo_engineer (3) ----
        new("p14_solo_engineer_au_dotnet", "First-engineer / solo-developer language, AU, C#/.NET.",
            "Nimbus Ventures", "Founding Engineer", "software", SponsorshipLine.None,
            "Australia", ArrangementLine.Remote, "Remote (AU)", 4, "4+ years", 125_000, "AUD", "$115,000 - $140,000 AUD",
            false, TeamSizeLine.SoloEngineer, ["C#", ".NET"], ["React"], [],
            StartupOrEnterprise, ProductEngineering, "You will be our first engineer, building the platform from the ground up.", ""),

        new("p15_solo_engineer_us_python", "Only-developer language, United States, Python.",
            "Basecamp Robotics", "Founding Software Engineer", "software", SponsorshipLine.None,
            "United States", ArrangementLine.Remote, "Remote (US)", 5, "5+ years", 135_000, "USD", "$120,000 - $150,000 USD",
            false, TeamSizeLine.SoloEngineer, ["Python", "Django"], ["React"], [],
            StartupOrEnterprise, ProductEngineering, "You will be our only developer, building the function from scratch.", ""),

        new("p16_solo_engineer_au_node", "First-engineer language, AU, Node/TS.",
            "Kestrel Labs", "Lead Engineer", "software", SponsorshipLine.None,
            "Australia", ArrangementLine.Remote, "Remote (AU)", 6, "6+ years", 140_000, "AUD", "$130,000 - $155,000 AUD",
            false, TeamSizeLine.SoloEngineer, ["Node.js", "TypeScript"], ["React"], [],
            StartupOrEnterprise, ProductEngineering, "First engineer role: build the engineering function from scratch.", ""),

        // ---- non-software professions: clean matches + one cross-category sponsorship test (3) ----
        new("p17_business_analyst_clean_au", "Clean business-analyst match, AU.",
            "Meridian Consulting Group", "Business Analyst", "business_analyst", SponsorshipLine.None,
            "Australia", ArrangementLine.Hybrid, "Melbourne, VIC", 5, "5+ years", 105_000, "AUD", "$95,000 - $115,000 AUD",
            false, TeamSizeLine.Unstated, ["Business analysis", "Requirements gathering"], ["Agile", "Jira"], [],
            ["Established mid-sized organisation with a structured PMO"], ["Clear ownership of a project end-to-end"],
            "Gather and document requirements for our claims processing platform.", ""),

        new("p18_registered_nurse_clean_au", "Clean registered-nurse match, AU on-site.",
            "St. Aldwyn Public Hospital", "Registered Nurse — Emergency", "nursing", SponsorshipLine.None,
            "Australia", ArrangementLine.OnSite, "Adelaide, SA", 4, "4+ years", 88_000, "AUD", "$80,000 - $95,000 AUD",
            false, TeamSizeLine.Unstated, ["Emergency", "ICU"], ["Registered Nurse license"], [],
            ["Public hospital with a structured clinical education program"], ["Direct patient care with clear scope"],
            "Join our Emergency Department nursing team.", ""),

        new("p19_teacher_clean_au", "Clean secondary-teacher match, AU on-site, no sponsorship language.",
            "Fairholme Secondary College", "Secondary Teacher — Mathematics", "teaching", SponsorshipLine.None,
            "Australia", ArrangementLine.OnSite, "Hobart, TAS", 4, "4+ years", 82_000, "AUD", "$75,000 - $90,000 AUD",
            false, TeamSizeLine.Unstated, ["Mathematics"], ["Australian Curriculum"], [],
            ["Established school with a mentoring program for new staff"], ["Full-time classroom teaching with a stable timetable"],
            "Teach Years 9-12 Mathematics.", ""),

        new("p20_teacher_sponsorship_not_offered_au", "Secondary-teacher posting with explicit no-sponsorship language, AU on-site.",
            "Brackenfield Grammar School", "Secondary Teacher — Science", "teaching", SponsorshipLine.NoSponsorshipOffered,
            "Australia", ArrangementLine.OnSite, "Canberra, ACT", 5, "5+ years", 85_000, "AUD", "$78,000 - $92,000 AUD",
            false, TeamSizeLine.Unstated, ["Science"], ["Australian Curriculum"], [],
            ["Established school with a mentoring program for new staff"], ["Full-time classroom teaching with a stable timetable"],
            "Teach Years 7-10 Science. Full working rights required, no visa sponsorship offered.", ""),

        // ---- clean spectrum + edge cases (10) ----
        new("p21_strong_match_au_dotnet", "Strong match across every dimension -- C#/.NET, target salary, ideal experience, product company.",
            "Copperfield Technologies", "Software Engineer", "software", SponsorshipLine.None,
            "Australia", ArrangementLine.Hybrid, "Melbourne, VIC", 3, "3 years", 115_000, "AUD", "$105,000 - $125,000 AUD",
            false, TeamSizeLine.SmallTeam, ["C#", ".NET", "ASP.NET Core"], ["React", "TypeScript"], [],
            ProductCompany, ProductEngineering, "Build and own checkout APIs for our e-commerce product, a Series B scale-up with paying customers.", ""),

        new("p22_acceptable_dimensions_flagged_low_salary", "Backend match acceptable-tier, frontend acceptable, salary flagged low.",
            "Thistledown Retail", "Software Engineer", "software", SponsorshipLine.None,
            "Australia", ArrangementLine.Hybrid, "Brisbane, QLD", 4, "4 years", 75_000, "AUD", "$70,000 - $80,000 AUD",
            false, TeamSizeLine.SmallTeam, ["C#", ".NET"], ["Angular"], [],
            StartupOrEnterprise, PlatformOrBackend, "Support our internal enterprise tooling team.", ""),

        new("p23_excluded_experience_range", "Backend/frontend strong match but experience requirement well above the excluded threshold.",
            "Ashworth Financial", "Senior Backend Engineer", "software", SponsorshipLine.None,
            "Australia", ArrangementLine.Hybrid, "Sydney, NSW", 12, "12+ years", 128_000, "AUD", "$118,000 - $138,000 AUD",
            false, TeamSizeLine.LargeTeam, ["C#", ".NET"], ["React"], [],
            ProductCompany, ProductEngineering, "Lead a team on our core lending platform.", ""),

        new("p24_missing_salary_au", "Salary entirely unstated -- edge case.",
            "Willowmere Studio", "Software Engineer", "software", SponsorshipLine.None,
            "Australia", ArrangementLine.Hybrid, "Melbourne, VIC", 3, "3+ years", null, "AUD", null,
            false, TeamSizeLine.SmallTeam, ["C#", ".NET"], ["React"], [],
            ProductCompany, ProductEngineering, "Build features for our design-tools product.", ""),

        new("p25_missing_location_au_stack", "Location entirely unstated -- edge case.",
            "Ferngrove Analytics", "Software Engineer", "software", SponsorshipLine.None,
            null, ArrangementLine.Unstated, null, 4, "4 years", 118_000, "AUD", "$110,000 - $126,000 AUD",
            false, TeamSizeLine.SmallTeam, ["C#", ".NET"], ["React"], [],
            ProductCompany, ProductEngineering, "Build our core analytics dashboards.", ""),

        new("p26_borderline_experience_midpoint", "Range midpoint sits exactly on an ideal/acceptable boundary -- edge case.",
            "Oakhaven Systems", "Software Engineer", "software", SponsorshipLine.None,
            "Australia", ArrangementLine.Hybrid, "Melbourne, VIC", 4, "3-5 years", 112_000, "AUD", "$100,000 - $124,000 AUD",
            false, TeamSizeLine.SmallTeam, ["C#", ".NET"], ["React"], [],
            ProductCompany, ProductEngineering, "Build APIs for our logistics platform.", ""),

        new("p27_flagged_high_salary_au", "Salary well above the flag-above threshold -- edge case (potential level mismatch).",
            "Bellcourt Holdings", "Software Engineer", "software", SponsorshipLine.None,
            "Australia", ArrangementLine.Hybrid, "Sydney, NSW", 4, "4 years", 210_000, "AUD", "$195,000 - $225,000 AUD",
            false, TeamSizeLine.LargeTeam, ["C#", ".NET"], ["React"], [],
            ProductCompany, ProductEngineering, "Build our core trading platform APIs.", ""),

        new("p28_clean_match_java_us_senior", "Clean match for c04 (Java/US senior).",
            "Kingswood Financial", "Senior Software Engineer", "software", SponsorshipLine.None,
            "United States", ArrangementLine.Hybrid, "New York, NY", 7, "7 years", 150_000, "USD", "$135,000 - $165,000 USD",
            false, TeamSizeLine.LargeTeam, ["Java", "Spring Boot"], ["Angular"], [],
            ProductCompany, ProductEngineering, "Own architecture for our risk-management platform.", ""),

        new("p29_clean_match_go_au", "Clean match for c06 (Go/AU) with a large, established team.",
            "Stonebridge Cloud", "Platform Engineer", "software", SponsorshipLine.None,
            "Australia", ArrangementLine.Remote, "Remote (AU)", 5, "5 years", 135_000, "AUD", "$125,000 - $145,000 AUD",
            false, TeamSizeLine.LargeTeam, ["Go", "Golang"], ["React"], [],
            ProductCompany, ProductEngineering, "Build our infrastructure platform on Go microservices.", ""),

        new("p30_missing_frontend_dimension", "Backend addressed, frontend entirely unaddressed -- 'missing' vs 'weak' edge case.",
            "Hollowick Data", "Backend Engineer", "software", SponsorshipLine.None,
            "Australia", ArrangementLine.Hybrid, "Perth, WA", 4, "4 years", 118_000, "AUD", "$108,000 - $128,000 AUD",
            false, TeamSizeLine.SmallTeam, ["C#", ".NET", "ASP.NET Core"], [], [],
            ProductCompany, ProductEngineering, "Own our data-ingestion backend services (pure API role, no frontend work).", ""),
    ];
}
