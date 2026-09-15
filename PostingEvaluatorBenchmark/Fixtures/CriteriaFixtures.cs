namespace PostingEvaluatorBenchmark.Fixtures;

// 20 synthetic candidate job-criteria profiles (no real user data), matching the
// UserProfile.JobCriteria YAML shape PostingEvaluator.BuildSystemPrompt interpolates verbatim
// (see skills/context/job_criteria.yaml and job_criteria_templates/*.yaml, which this mirrors
// structurally). 17 software-engineering profiles varying the axes evaluate_posting.md actually
// scores (sponsorship/citizenship stance, location + remote/hybrid stance, seniority range,
// preferred backend/frontend stack, salary range + currency, company-type and role-type
// preference, which disqualifiers are authored at all) plus 3 non-software profiles (business
// analyst, registered nurse, secondary teacher) to exercise evaluate_posting.md's profession-
// agnostic design and the built-in profession_mismatch safety net.
//
// c13/c14/c15 deliberately use the CURRENT flat `skills:` list format (names only, no
// candidate-authored tiers) -- every other profile uses the "legacy" richer `skill_dimensions:`
// format with explicit strong/good/acceptable/excluded term lists, which is what lets
// GroundTruthResolver grade skill-tier accuracy objectively (see that class's doc comment). Both
// shapes are real, current production shapes -- evaluate_posting.md explicitly still supports the
// legacy shape for accounts that haven't re-saved criteria since #127 simplified new saves to the
// flat list.
public static class CriteriaFixtures
{
    // Shared, reused keyword vocabulary -- see GroundTruthResolver.ResolveTierLookup: matching is
    // exact (case-insensitive) keyword membership, so postings that want a specific company/role
    // tier just need to include the matching literal phrase from these lists.
    private static readonly string[] ProductCompany = ["Product company with paying customers", "Series B scale-up", "Series C scale-up"];
    private static readonly string[] StartupOrEnterprise = ["Early-stage startup with product-market fit", "Enterprise team with clear scope"];
    private static readonly string[] AgencyOrConsultancy = ["Agency", "Consultancy"];
    private static readonly string[] PreRevenue = ["Pre-revenue startup"];

    private static readonly string[] ProductEngineering = ["Product engineering, owning features end-to-end"];
    private static readonly string[] PlatformOrBackend = ["Platform engineering with product impact", "Backend-focused with some frontend"];
    private static readonly string[] AgencyProjectWork = ["Rotating agency project work"];
    private static readonly string[] PureMaintenance = ["Pure legacy maintenance, no modernisation"];

    public static readonly IReadOnlyList<CriteriaProfile> All =
    [
        new(
            Id: "c01_dotnet_au_citizen",
            Description: "Citizen/PR, AU, mid-level, C#/.NET, AUD target range, legacy skill_dimensions format.",
            ProfessionFamily: "software",
            ProfessionCategoryLabel: "Engineering — Software",
            TargetJobTitles: "Software Engineer, Backend Developer",
            Sponsorship: SponsorshipStance.CitizenOrPr,
            PreferredCountry: "Australia", RemoteAccepted: true, HybridAccepted: true,
            IdealMaxYears: 4, AcceptableMaxYears: 6,
            SalaryCurrency: "AUD", TargetLow: 100_000, TargetHigh: 130_000, FlagBelow: 90_000, FlagAbove: 150_000,
            Skills:
            [
                new("Backend stack", Strong: ["C#", ".NET", "ASP.NET Core"], Good: [], Acceptable: [], Excluded: ["PHP", "Laravel", "Java", "Spring Boot", "Python", "Django", "Node.js"]),
                new("Frontend stack", Strong: ["React", "TypeScript"], Good: ["Vue.js"], Acceptable: ["Angular"], Excluded: []),
            ],
            PrimaryDisqualifierId: "php_primary",
            CompanyPreferred: ProductCompany, CompanyAcceptable: StartupOrEnterprise, CompanyWeaker: AgencyOrConsultancy, CompanyExcluded: PreRevenue,
            RoleTypePreferred: ProductEngineering, RoleTypeAcceptable: PlatformOrBackend, RoleTypeWeaker: AgencyProjectWork, RoleTypeExcluded: PureMaintenance,
            IncludeGamblingDisqualifier: true, IncludeSoloEngineerDisqualifier: true, UseLegacySkillDimensionsFormat: true),

        new(
            Id: "c02_node_au_nonpr_hasvisa",
            Description: "Non-citizen with a current work visa (sponsorship_not_offered doesn't apply), AU remote-only, Node/TS.",
            ProfessionFamily: "software",
            ProfessionCategoryLabel: "Engineering — Software",
            TargetJobTitles: "Software Engineer, Full Stack Developer",
            Sponsorship: SponsorshipStance.NonCitizenHasVisa,
            PreferredCountry: "Australia", RemoteAccepted: true, HybridAccepted: false,
            IdealMaxYears: 3, AcceptableMaxYears: 5,
            SalaryCurrency: "AUD", TargetLow: 90_000, TargetHigh: 120_000, FlagBelow: 80_000, FlagAbove: 140_000,
            Skills:
            [
                new("Backend stack", Strong: ["Node.js", "TypeScript"], Good: ["Express"], Acceptable: [], Excluded: ["PHP", "Laravel", "Java", "Spring Boot", ".NET", "C#"]),
                new("Frontend stack", Strong: ["React"], Good: ["Next.js"], Acceptable: [], Excluded: []),
            ],
            PrimaryDisqualifierId: "php_primary",
            CompanyPreferred: ProductCompany, CompanyAcceptable: StartupOrEnterprise, CompanyWeaker: AgencyOrConsultancy, CompanyExcluded: PreRevenue,
            RoleTypePreferred: ProductEngineering, RoleTypeAcceptable: PlatformOrBackend, RoleTypeWeaker: AgencyProjectWork, RoleTypeExcluded: PureMaintenance,
            IncludeGamblingDisqualifier: true, IncludeSoloEngineerDisqualifier: true, UseLegacySkillDimensionsFormat: true),

        new(
            Id: "c03_python_au_nonpr_novisa",
            Description: "Non-citizen, no current work visa (both sponsorship checks apply), AU hybrid-only, Python/Django.",
            ProfessionFamily: "software",
            ProfessionCategoryLabel: "Engineering — Software",
            TargetJobTitles: "Backend Engineer, Software Engineer",
            Sponsorship: SponsorshipStance.NonCitizenNoVisa,
            PreferredCountry: "Australia", RemoteAccepted: false, HybridAccepted: true,
            IdealMaxYears: 5, AcceptableMaxYears: 8,
            SalaryCurrency: "AUD", TargetLow: 110_000, TargetHigh: 150_000, FlagBelow: 100_000, FlagAbove: 170_000,
            Skills:
            [
                new("Backend stack", Strong: ["Python", "Django"], Good: ["Flask", "FastAPI"], Acceptable: [], Excluded: ["PHP", "Laravel", "Java", "Spring Boot", ".NET", "C#", "Node.js"]),
                new("Frontend stack", Strong: ["Vue.js"], Good: ["React"], Acceptable: [], Excluded: []),
            ],
            PrimaryDisqualifierId: "php_primary",
            CompanyPreferred: ProductCompany, CompanyAcceptable: StartupOrEnterprise, CompanyWeaker: AgencyOrConsultancy, CompanyExcluded: PreRevenue,
            RoleTypePreferred: ProductEngineering, RoleTypeAcceptable: PlatformOrBackend, RoleTypeWeaker: AgencyProjectWork, RoleTypeExcluded: PureMaintenance,
            IncludeGamblingDisqualifier: true, IncludeSoloEngineerDisqualifier: true, UseLegacySkillDimensionsFormat: true),

        new(
            Id: "c04_java_us_citizen_senior",
            Description: "Citizen/PR, United States, senior range, Java/Spring, USD.",
            ProfessionFamily: "software",
            ProfessionCategoryLabel: "Engineering — Software",
            TargetJobTitles: "Senior Software Engineer, Staff Engineer",
            Sponsorship: SponsorshipStance.CitizenOrPr,
            PreferredCountry: "United States", RemoteAccepted: true, HybridAccepted: true,
            IdealMaxYears: 6, AcceptableMaxYears: 9,
            SalaryCurrency: "USD", TargetLow: 130_000, TargetHigh: 170_000, FlagBelow: 120_000, FlagAbove: 190_000,
            Skills:
            [
                new("Backend stack", Strong: ["Java", "Spring Boot"], Good: ["Kotlin"], Acceptable: [], Excluded: ["PHP", "Laravel", "Python", "Django", "Node.js", "Ruby"]),
                new("Frontend stack", Strong: ["Angular"], Good: ["React"], Acceptable: [], Excluded: []),
            ],
            PrimaryDisqualifierId: "php_primary",
            CompanyPreferred: ProductCompany, CompanyAcceptable: StartupOrEnterprise, CompanyWeaker: AgencyOrConsultancy, CompanyExcluded: PreRevenue,
            RoleTypePreferred: ProductEngineering, RoleTypeAcceptable: PlatformOrBackend, RoleTypeWeaker: AgencyProjectWork, RoleTypeExcluded: PureMaintenance,
            IncludeGamblingDisqualifier: true, IncludeSoloEngineerDisqualifier: false, UseLegacySkillDimensionsFormat: true),

        new(
            Id: "c05_ruby_uk_unanswered",
            Description: "Sponsorship section unanswered (behaves as citizen/PR), United Kingdom, Ruby/Rails, GBP.",
            ProfessionFamily: "software",
            ProfessionCategoryLabel: "Engineering — Software",
            TargetJobTitles: "Software Engineer, Backend Developer",
            Sponsorship: SponsorshipStance.Unanswered,
            PreferredCountry: "United Kingdom", RemoteAccepted: true, HybridAccepted: true,
            IdealMaxYears: 4, AcceptableMaxYears: 6,
            SalaryCurrency: "GBP", TargetLow: 55_000, TargetHigh: 75_000, FlagBelow: 48_000, FlagAbove: 85_000,
            Skills:
            [
                new("Backend stack", Strong: ["Ruby", "Ruby on Rails"], Good: [], Acceptable: [], Excluded: ["PHP", "Laravel", "Java", "Spring Boot", "Python"]),
                new("Frontend stack", Strong: ["React"], Good: ["Stimulus"], Acceptable: [], Excluded: []),
            ],
            PrimaryDisqualifierId: "php_primary",
            CompanyPreferred: ProductCompany, CompanyAcceptable: StartupOrEnterprise, CompanyWeaker: AgencyOrConsultancy, CompanyExcluded: PreRevenue,
            RoleTypePreferred: ProductEngineering, RoleTypeAcceptable: PlatformOrBackend, RoleTypeWeaker: AgencyProjectWork, RoleTypeExcluded: PureMaintenance,
            IncludeGamblingDisqualifier: true, IncludeSoloEngineerDisqualifier: true, UseLegacySkillDimensionsFormat: true),

        new(
            Id: "c06_go_au_nonpr_novisa",
            Description: "Non-citizen, no work visa, AU, Go.",
            ProfessionFamily: "software",
            ProfessionCategoryLabel: "Engineering — Software",
            TargetJobTitles: "Backend Engineer, Platform Engineer",
            Sponsorship: SponsorshipStance.NonCitizenNoVisa,
            PreferredCountry: "Australia", RemoteAccepted: true, HybridAccepted: true,
            IdealMaxYears: 5, AcceptableMaxYears: 7,
            SalaryCurrency: "AUD", TargetLow: 120_000, TargetHigh: 150_000, FlagBelow: 105_000, FlagAbove: 170_000,
            Skills:
            [
                new("Backend stack", Strong: ["Go", "Golang"], Good: [], Acceptable: [], Excluded: ["PHP", "Laravel", "Java", "Spring Boot", ".NET", "C#", "Python", "Node.js"]),
                new("Frontend stack", Strong: ["React"], Good: [], Acceptable: ["Any modern JS framework"], Excluded: []),
            ],
            PrimaryDisqualifierId: "php_primary",
            CompanyPreferred: ProductCompany, CompanyAcceptable: StartupOrEnterprise, CompanyWeaker: AgencyOrConsultancy, CompanyExcluded: PreRevenue,
            RoleTypePreferred: ProductEngineering, RoleTypeAcceptable: PlatformOrBackend, RoleTypeWeaker: AgencyProjectWork, RoleTypeExcluded: PureMaintenance,
            IncludeGamblingDisqualifier: true, IncludeSoloEngineerDisqualifier: true, UseLegacySkillDimensionsFormat: true),

        new(
            Id: "c07_php_preferring_au_citizen",
            Description: "PHP is the PREFERRED stack (not excluded) -- negative-testing fixture for php_primary false positives.",
            ProfessionFamily: "software",
            ProfessionCategoryLabel: "Engineering — Software",
            TargetJobTitles: "Backend Developer, Software Engineer",
            Sponsorship: SponsorshipStance.CitizenOrPr,
            PreferredCountry: "Australia", RemoteAccepted: true, HybridAccepted: true,
            IdealMaxYears: 4, AcceptableMaxYears: 6,
            SalaryCurrency: "AUD", TargetLow: 95_000, TargetHigh: 125_000, FlagBelow: 85_000, FlagAbove: 145_000,
            Skills:
            [
                new("Backend stack", Strong: ["PHP", "Laravel"], Good: ["Symfony"], Acceptable: [], Excluded: ["Java", "Spring Boot", "Python", "Django", "Ruby", "Go"]),
                new("Frontend stack", Strong: ["Vue.js"], Good: ["React"], Acceptable: [], Excluded: []),
            ],
            PrimaryDisqualifierId: "backend_stack_excluded",
            CompanyPreferred: ProductCompany, CompanyAcceptable: StartupOrEnterprise, CompanyWeaker: AgencyOrConsultancy, CompanyExcluded: PreRevenue,
            RoleTypePreferred: ProductEngineering, RoleTypeAcceptable: PlatformOrBackend, RoleTypeWeaker: AgencyProjectWork, RoleTypeExcluded: PureMaintenance,
            IncludeGamblingDisqualifier: true, IncludeSoloEngineerDisqualifier: true, UseLegacySkillDimensionsFormat: true),

        new(
            Id: "c08_dotnet_au_nonpr_hasvisa_senior",
            Description: "Non-citizen with current visa, AU, senior experience range, C#/.NET.",
            ProfessionFamily: "software",
            ProfessionCategoryLabel: "Engineering — Software",
            TargetJobTitles: "Senior Software Engineer, Staff Engineer",
            Sponsorship: SponsorshipStance.NonCitizenHasVisa,
            PreferredCountry: "Australia", RemoteAccepted: true, HybridAccepted: true,
            IdealMaxYears: 7, AcceptableMaxYears: 10,
            SalaryCurrency: "AUD", TargetLow: 140_000, TargetHigh: 180_000, FlagBelow: 125_000, FlagAbove: 200_000,
            Skills:
            [
                new("Backend stack", Strong: ["C#", ".NET", "ASP.NET Core"], Good: [], Acceptable: [], Excluded: ["PHP", "Laravel", "Java", "Python", "Node.js"]),
                new("Frontend stack", Strong: ["React"], Good: ["Angular"], Acceptable: [], Excluded: []),
            ],
            PrimaryDisqualifierId: "php_primary",
            CompanyPreferred: ProductCompany, CompanyAcceptable: StartupOrEnterprise, CompanyWeaker: AgencyOrConsultancy, CompanyExcluded: PreRevenue,
            RoleTypePreferred: ProductEngineering, RoleTypeAcceptable: PlatformOrBackend, RoleTypeWeaker: AgencyProjectWork, RoleTypeExcluded: PureMaintenance,
            IncludeGamblingDisqualifier: true, IncludeSoloEngineerDisqualifier: true, UseLegacySkillDimensionsFormat: true),

        new(
            Id: "c09_dotnet_au_citizen_junior",
            Description: "Citizen/PR, AU, junior experience range, C#/.NET.",
            ProfessionFamily: "software",
            ProfessionCategoryLabel: "Engineering — Software",
            TargetJobTitles: "Junior Software Engineer, Graduate Developer",
            Sponsorship: SponsorshipStance.CitizenOrPr,
            PreferredCountry: "Australia", RemoteAccepted: true, HybridAccepted: true,
            IdealMaxYears: 2, AcceptableMaxYears: 3,
            SalaryCurrency: "AUD", TargetLow: 70_000, TargetHigh: 90_000, FlagBelow: 62_000, FlagAbove: 100_000,
            Skills:
            [
                new("Backend stack", Strong: ["C#", ".NET"], Good: [], Acceptable: [], Excluded: ["PHP", "Laravel", "Java", "Python", "Node.js"]),
                new("Frontend stack", Strong: ["React"], Good: [], Acceptable: [], Excluded: []),
            ],
            PrimaryDisqualifierId: "php_primary",
            CompanyPreferred: ProductCompany, CompanyAcceptable: StartupOrEnterprise, CompanyWeaker: AgencyOrConsultancy, CompanyExcluded: PreRevenue,
            RoleTypePreferred: ProductEngineering, RoleTypeAcceptable: PlatformOrBackend, RoleTypeWeaker: AgencyProjectWork, RoleTypeExcluded: PureMaintenance,
            IncludeGamblingDisqualifier: true, IncludeSoloEngineerDisqualifier: true, UseLegacySkillDimensionsFormat: true),

        new(
            Id: "c10_node_us_nonpr_novisa",
            Description: "Non-citizen, no work visa, United States remote-only, Node/TS.",
            ProfessionFamily: "software",
            ProfessionCategoryLabel: "Engineering — Software",
            TargetJobTitles: "Full Stack Developer, Software Engineer",
            Sponsorship: SponsorshipStance.NonCitizenNoVisa,
            PreferredCountry: "United States", RemoteAccepted: true, HybridAccepted: false,
            IdealMaxYears: 4, AcceptableMaxYears: 6,
            SalaryCurrency: "USD", TargetLow: 100_000, TargetHigh: 140_000, FlagBelow: 90_000, FlagAbove: 160_000,
            Skills:
            [
                new("Backend stack", Strong: ["Node.js", "TypeScript"], Good: [], Acceptable: [], Excluded: ["PHP", "Laravel", "Java", ".NET", "C#", "Python"]),
                new("Frontend stack", Strong: ["React"], Good: ["Next.js"], Acceptable: [], Excluded: []),
            ],
            PrimaryDisqualifierId: "php_primary",
            CompanyPreferred: ProductCompany, CompanyAcceptable: StartupOrEnterprise, CompanyWeaker: AgencyOrConsultancy, CompanyExcluded: PreRevenue,
            RoleTypePreferred: ProductEngineering, RoleTypeAcceptable: PlatformOrBackend, RoleTypeWeaker: AgencyProjectWork, RoleTypeExcluded: PureMaintenance,
            IncludeGamblingDisqualifier: true, IncludeSoloEngineerDisqualifier: true, UseLegacySkillDimensionsFormat: true),

        new(
            Id: "c11_dotnet_ca_citizen",
            Description: "Citizen/PR, Canada, hybrid-only, C#/.NET, CAD.",
            ProfessionFamily: "software",
            ProfessionCategoryLabel: "Engineering — Software",
            TargetJobTitles: "Software Engineer, Backend Developer",
            Sponsorship: SponsorshipStance.CitizenOrPr,
            PreferredCountry: "Canada", RemoteAccepted: false, HybridAccepted: true,
            IdealMaxYears: 5, AcceptableMaxYears: 7,
            SalaryCurrency: "CAD", TargetLow: 100_000, TargetHigh: 130_000, FlagBelow: 88_000, FlagAbove: 145_000,
            Skills:
            [
                new("Backend stack", Strong: ["C#", ".NET"], Good: [], Acceptable: [], Excluded: ["PHP", "Laravel", "Java", "Python", "Node.js"]),
                new("Frontend stack", Strong: ["Angular"], Good: ["React"], Acceptable: [], Excluded: []),
            ],
            PrimaryDisqualifierId: "php_primary",
            CompanyPreferred: ProductCompany, CompanyAcceptable: StartupOrEnterprise, CompanyWeaker: AgencyOrConsultancy, CompanyExcluded: PreRevenue,
            RoleTypePreferred: ProductEngineering, RoleTypeAcceptable: PlatformOrBackend, RoleTypeWeaker: AgencyProjectWork, RoleTypeExcluded: PureMaintenance,
            IncludeGamblingDisqualifier: true, IncludeSoloEngineerDisqualifier: true, UseLegacySkillDimensionsFormat: true),

        new(
            Id: "c12_python_au_citizen_3dim_cloud",
            Description: "Citizen/PR, AU, Python/Django, 3rd skill dimension (Cloud platform).",
            ProfessionFamily: "software",
            ProfessionCategoryLabel: "Engineering — Software",
            TargetJobTitles: "Backend Engineer, Software Engineer",
            Sponsorship: SponsorshipStance.CitizenOrPr,
            PreferredCountry: "Australia", RemoteAccepted: true, HybridAccepted: true,
            IdealMaxYears: 4, AcceptableMaxYears: 6,
            SalaryCurrency: "AUD", TargetLow: 115_000, TargetHigh: 145_000, FlagBelow: 100_000, FlagAbove: 165_000,
            Skills:
            [
                new("Backend stack", Strong: ["Python", "Django"], Good: [], Acceptable: [], Excluded: ["PHP", "Laravel", "Java", ".NET", "C#", "Node.js"]),
                new("Frontend stack", Strong: ["React"], Good: [], Acceptable: [], Excluded: []),
                new("Cloud platform", Strong: ["AWS"], Good: ["GCP"], Acceptable: ["Azure"], Excluded: []),
            ],
            PrimaryDisqualifierId: "php_primary",
            CompanyPreferred: ProductCompany, CompanyAcceptable: StartupOrEnterprise, CompanyWeaker: AgencyOrConsultancy, CompanyExcluded: PreRevenue,
            RoleTypePreferred: ProductEngineering, RoleTypeAcceptable: PlatformOrBackend, RoleTypeWeaker: AgencyProjectWork, RoleTypeExcluded: PureMaintenance,
            IncludeGamblingDisqualifier: true, IncludeSoloEngineerDisqualifier: true, UseLegacySkillDimensionsFormat: true),

        new(
            Id: "c13_dotnet_au_flatlist",
            Description: "Citizen/PR, AU, C#/.NET -- CURRENT flat skills: list format (structural coverage only, no exact skill-tier grading).",
            ProfessionFamily: "software",
            ProfessionCategoryLabel: "Engineering — Software",
            TargetJobTitles: "Software Engineer, Backend Developer",
            Sponsorship: SponsorshipStance.CitizenOrPr,
            PreferredCountry: "Australia", RemoteAccepted: true, HybridAccepted: true,
            IdealMaxYears: 4, AcceptableMaxYears: 6,
            SalaryCurrency: "AUD", TargetLow: 100_000, TargetHigh: 130_000, FlagBelow: 90_000, FlagAbove: 150_000,
            Skills:
            [
                new("Backend stack", Strong: ["C#", ".NET"], Good: [], Acceptable: [], Excluded: ["PHP", "Laravel", "Java", "Python", "Node.js"]),
                new("Frontend stack", Strong: ["React"], Good: [], Acceptable: [], Excluded: []),
            ],
            PrimaryDisqualifierId: "php_primary",
            CompanyPreferred: ProductCompany, CompanyAcceptable: StartupOrEnterprise, CompanyWeaker: AgencyOrConsultancy, CompanyExcluded: PreRevenue,
            RoleTypePreferred: ProductEngineering, RoleTypeAcceptable: PlatformOrBackend, RoleTypeWeaker: AgencyProjectWork, RoleTypeExcluded: PureMaintenance,
            IncludeGamblingDisqualifier: true, IncludeSoloEngineerDisqualifier: true, UseLegacySkillDimensionsFormat: false),

        new(
            Id: "c14_node_au_flatlist_nonpr_novisa",
            Description: "Non-citizen, no work visa, AU, Node/TS -- flat skills: list format.",
            ProfessionFamily: "software",
            ProfessionCategoryLabel: "Engineering — Software",
            TargetJobTitles: "Full Stack Developer, Software Engineer",
            Sponsorship: SponsorshipStance.NonCitizenNoVisa,
            PreferredCountry: "Australia", RemoteAccepted: true, HybridAccepted: true,
            IdealMaxYears: 4, AcceptableMaxYears: 6,
            SalaryCurrency: "AUD", TargetLow: 100_000, TargetHigh: 130_000, FlagBelow: 90_000, FlagAbove: 150_000,
            Skills:
            [
                new("Backend stack", Strong: ["Node.js", "TypeScript"], Good: [], Acceptable: [], Excluded: ["PHP", "Laravel", "Java", ".NET", "C#", "Python"]),
                new("Frontend stack", Strong: ["React"], Good: [], Acceptable: [], Excluded: []),
            ],
            PrimaryDisqualifierId: "php_primary",
            CompanyPreferred: ProductCompany, CompanyAcceptable: StartupOrEnterprise, CompanyWeaker: AgencyOrConsultancy, CompanyExcluded: PreRevenue,
            RoleTypePreferred: ProductEngineering, RoleTypeAcceptable: PlatformOrBackend, RoleTypeWeaker: AgencyProjectWork, RoleTypeExcluded: PureMaintenance,
            IncludeGamblingDisqualifier: true, IncludeSoloEngineerDisqualifier: true, UseLegacySkillDimensionsFormat: false),

        new(
            Id: "c15_dotnet_us_flatlist_remote",
            Description: "Citizen/PR, United States, remote-first, C#/.NET -- flat skills: list format, USD.",
            ProfessionFamily: "software",
            ProfessionCategoryLabel: "Engineering — Software",
            TargetJobTitles: "Software Engineer, Backend Developer",
            Sponsorship: SponsorshipStance.CitizenOrPr,
            PreferredCountry: "United States", RemoteAccepted: true, HybridAccepted: false,
            IdealMaxYears: 6, AcceptableMaxYears: 9,
            SalaryCurrency: "USD", TargetLow: 120_000, TargetHigh: 160_000, FlagBelow: 105_000, FlagAbove: 180_000,
            Skills:
            [
                new("Backend stack", Strong: ["C#", ".NET"], Good: [], Acceptable: [], Excluded: ["PHP", "Laravel", "Java", "Python", "Node.js"]),
                new("Frontend stack", Strong: ["React"], Good: [], Acceptable: [], Excluded: []),
            ],
            PrimaryDisqualifierId: "php_primary",
            CompanyPreferred: ProductCompany, CompanyAcceptable: StartupOrEnterprise, CompanyWeaker: AgencyOrConsultancy, CompanyExcluded: PreRevenue,
            RoleTypePreferred: ProductEngineering, RoleTypeAcceptable: PlatformOrBackend, RoleTypeWeaker: AgencyProjectWork, RoleTypeExcluded: PureMaintenance,
            IncludeGamblingDisqualifier: true, IncludeSoloEngineerDisqualifier: true, UseLegacySkillDimensionsFormat: false),

        new(
            Id: "c16_dotnet_au_citizen_no_gambling_no_solo",
            Description: "Citizen/PR, AU, C#/.NET -- deliberately does NOT author gambling_core or solo_engineer (tests they correctly don't fire when unauthored).",
            ProfessionFamily: "software",
            ProfessionCategoryLabel: "Engineering — Software",
            TargetJobTitles: "Software Engineer, Backend Developer",
            Sponsorship: SponsorshipStance.CitizenOrPr,
            PreferredCountry: "Australia", RemoteAccepted: true, HybridAccepted: true,
            IdealMaxYears: 4, AcceptableMaxYears: 6,
            SalaryCurrency: "AUD", TargetLow: 100_000, TargetHigh: 130_000, FlagBelow: 90_000, FlagAbove: 150_000,
            Skills:
            [
                new("Backend stack", Strong: ["C#", ".NET"], Good: [], Acceptable: [], Excluded: ["PHP", "Laravel", "Java", "Python", "Node.js"]),
                new("Frontend stack", Strong: ["React"], Good: [], Acceptable: [], Excluded: []),
                new("Cloud platform", Strong: ["Azure"], Good: ["AWS"], Acceptable: ["GCP"], Excluded: []),
            ],
            PrimaryDisqualifierId: "php_primary",
            CompanyPreferred: ProductCompany, CompanyAcceptable: StartupOrEnterprise, CompanyWeaker: AgencyOrConsultancy, CompanyExcluded: PreRevenue,
            RoleTypePreferred: ProductEngineering, RoleTypeAcceptable: PlatformOrBackend, RoleTypeWeaker: AgencyProjectWork, RoleTypeExcluded: PureMaintenance,
            IncludeGamblingDisqualifier: false, IncludeSoloEngineerDisqualifier: false, UseLegacySkillDimensionsFormat: true),

        new(
            Id: "c17_dotnet_au_citizen_3dim_testing",
            Description: "Citizen/PR, AU, C#/.NET, 3rd skill dimension (Testing practices).",
            ProfessionFamily: "software",
            ProfessionCategoryLabel: "Engineering — Software",
            TargetJobTitles: "Software Engineer, QA-minded Backend Developer",
            Sponsorship: SponsorshipStance.CitizenOrPr,
            PreferredCountry: "Australia", RemoteAccepted: true, HybridAccepted: true,
            IdealMaxYears: 5, AcceptableMaxYears: 7,
            SalaryCurrency: "AUD", TargetLow: 110_000, TargetHigh: 140_000, FlagBelow: 95_000, FlagAbove: 160_000,
            Skills:
            [
                new("Backend stack", Strong: ["C#", ".NET"], Good: [], Acceptable: [], Excluded: ["PHP", "Laravel", "Java", "Python", "Node.js"]),
                new("Frontend stack", Strong: ["React"], Good: [], Acceptable: [], Excluded: []),
                new("Testing practices", Strong: ["xUnit", "NUnit"], Good: ["Jest"], Acceptable: [], Excluded: []),
            ],
            PrimaryDisqualifierId: "php_primary",
            CompanyPreferred: ProductCompany, CompanyAcceptable: StartupOrEnterprise, CompanyWeaker: AgencyOrConsultancy, CompanyExcluded: PreRevenue,
            RoleTypePreferred: ProductEngineering, RoleTypeAcceptable: PlatformOrBackend, RoleTypeWeaker: AgencyProjectWork, RoleTypeExcluded: PureMaintenance,
            IncludeGamblingDisqualifier: true, IncludeSoloEngineerDisqualifier: true, UseLegacySkillDimensionsFormat: true),

        // ---- Non-software professions: profession-agnostic skill dimensions is the whole
        // point of evaluate_posting.md's design, and profession_mismatch (a built-in safety net,
        // not authored by the candidate) is graded by pairing these against software postings
        // in BenchmarkSuite.cs. ----

        new(
            Id: "c18_business_analyst_au_citizen",
            Description: "Citizen/PR, AU, business analyst -- non-software profession, legacy skill_dimensions format.",
            ProfessionFamily: "business_analyst",
            ProfessionCategoryLabel: "Corporate — Non-Technical (Business Analyst)",
            TargetJobTitles: "Business Analyst, Product Analyst",
            Sponsorship: SponsorshipStance.CitizenOrPr,
            PreferredCountry: "Australia", RemoteAccepted: true, HybridAccepted: true,
            IdealMaxYears: 5, AcceptableMaxYears: 8,
            SalaryCurrency: "AUD", TargetLow: 90_000, TargetHigh: 120_000, FlagBelow: 78_000, FlagAbove: 135_000,
            Skills:
            [
                new("Functional specialization", Strong: ["Business analysis", "Requirements gathering"], Good: ["Process improvement"], Acceptable: [], Excluded: []),
                new("Methodology/tooling", Strong: ["Agile", "Jira"], Good: ["Waterfall", "Confluence"], Acceptable: [], Excluded: []),
            ],
            PrimaryDisqualifierId: "unsupported_methodology",
            CompanyPreferred: ["Established mid-sized organisation with a structured PMO"], CompanyAcceptable: ["Consultancy with a long-term embedded engagement"], CompanyWeaker: ["Short rotating consultancy placements"], CompanyExcluded: [],
            RoleTypePreferred: ["Clear ownership of a project end-to-end"], RoleTypeAcceptable: ["Coordination with some analysis ownership"], RoleTypeWeaker: ["Purely administrative coordination"], RoleTypeExcluded: [],
            IncludeGamblingDisqualifier: true, IncludeSoloEngineerDisqualifier: true, UseLegacySkillDimensionsFormat: true),

        new(
            Id: "c19_registered_nurse_au_citizen",
            Description: "Citizen/PR, AU, on-site only, registered nurse -- non-software profession.",
            ProfessionFamily: "nursing",
            ProfessionCategoryLabel: "Health Care — Nursing",
            TargetJobTitles: "Registered Nurse, Clinical Nurse",
            Sponsorship: SponsorshipStance.CitizenOrPr,
            PreferredCountry: "Australia", RemoteAccepted: false, HybridAccepted: false,
            IdealMaxYears: 6, AcceptableMaxYears: 10,
            SalaryCurrency: "AUD", TargetLow: 80_000, TargetHigh: 100_000, FlagBelow: 70_000, FlagAbove: 115_000,
            Skills:
            [
                new("Clinical specialty", Strong: ["Emergency", "ICU"], Good: ["General ward"], Acceptable: [], Excluded: []),
                new("Certification", Strong: ["Registered Nurse license"], Good: [], Acceptable: [], Excluded: []),
            ],
            PrimaryDisqualifierId: "unsupported_specialty",
            CompanyPreferred: ["Public hospital with a structured clinical education program"], CompanyAcceptable: ["Private hospital"], CompanyWeaker: ["Agency staffing pool"], CompanyExcluded: [],
            RoleTypePreferred: ["Direct patient care with clear scope"], RoleTypeAcceptable: ["Rotating ward assignment"], RoleTypeWeaker: ["Administrative-only nursing role"], RoleTypeExcluded: [],
            IncludeGamblingDisqualifier: false, IncludeSoloEngineerDisqualifier: false, UseLegacySkillDimensionsFormat: true),

        new(
            Id: "c20_secondary_teacher_au_nonpr_novisa",
            Description: "Non-citizen, no work visa, AU, secondary teacher -- non-software profession.",
            ProfessionFamily: "teaching",
            ProfessionCategoryLabel: "Education — Secondary Teaching",
            TargetJobTitles: "Secondary Teacher, Subject Teacher",
            Sponsorship: SponsorshipStance.NonCitizenNoVisa,
            PreferredCountry: "Australia", RemoteAccepted: false, HybridAccepted: false,
            IdealMaxYears: 5, AcceptableMaxYears: 8,
            SalaryCurrency: "AUD", TargetLow: 75_000, TargetHigh: 95_000, FlagBelow: 65_000, FlagAbove: 105_000,
            Skills:
            [
                new("Subject specialization", Strong: ["Mathematics", "Science"], Good: ["English"], Acceptable: [], Excluded: []),
                new("Curriculum framework", Strong: ["Australian Curriculum"], Good: ["IB"], Acceptable: [], Excluded: []),
            ],
            PrimaryDisqualifierId: "unsupported_subject",
            CompanyPreferred: ["Established school with a mentoring program for new staff"], CompanyAcceptable: ["Independent school"], CompanyWeaker: ["Relief/casual-only teaching pool"], CompanyExcluded: [],
            RoleTypePreferred: ["Full-time classroom teaching with a stable timetable"], RoleTypeAcceptable: ["Part-time classroom teaching"], RoleTypeWeaker: ["Casual relief teaching only"], RoleTypeExcluded: [],
            IncludeGamblingDisqualifier: false, IncludeSoloEngineerDisqualifier: false, UseLegacySkillDimensionsFormat: true),
    ];
}
