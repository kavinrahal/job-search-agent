namespace JobSearch.Data;

public enum ResumeSeniority { Junior, Experienced }

// One default SectionConfig ordering per industry researched for the resume-builder Phase 2
// ticket (Notion "Resume builder", 9 industries surveyed). Deliberately just a reordering/
// inclusion toggle over the 7 section keys ResumeRenderer/ResumeOverrideSchema already know
// (experience/education/skills/projects/credentials/publications/volunteering) — the prior
// phase generalized Credentials' `kind` enum (license/bar_admission/certification) specifically
// so this phase wouldn't need new section keys or a new DB table for per-industry "gate" blocks.
public record ResumeIndustryTemplate(
    string Key,
    string DisplayName,
    bool HasSeniorityToggle,
    IReadOnlyList<SectionConfigEntry> JuniorOrder,
    IReadOnlyList<SectionConfigEntry> ExperiencedOrder)
{
    public IReadOnlyList<SectionConfigEntry> OrderFor(ResumeSeniority seniority) =>
        seniority == ResumeSeniority.Junior ? JuniorOrder : ExperiencedOrder;
}

public static class ResumeIndustryTemplates
{
    // Identical to ResumeRenderer's own default (Experience leads, no gate section) — the shape
    // research found for every industry with nothing distinguishing to insert above Experience.
    private static readonly IReadOnlyList<SectionConfigEntry> Baseline = ResumeRenderer.DefaultSectionConfig;

    // Finance/Consulting and Sales/Retail junior-hire order: Education (GPA, honors) promoted
    // above Experience. Projects/Publications/Volunteering inclusion is left at baseline — only
    // the researched gate section's position/inclusion changes, nothing else was found to differ.
    private static readonly IReadOnlyList<SectionConfigEntry> EducationGateOrder =
    [
        new("education", true), new("experience", true), new("skills", true), new("projects", true),
        new("credentials", false), new("publications", false), new("volunteering", true),
    ];

    // Legal junior: Education + mandatory Bar Admissions (credentials) both gate above Experience.
    private static readonly IReadOnlyList<SectionConfigEntry> LegalJuniorOrder =
    [
        new("education", true), new("credentials", true), new("experience", true), new("skills", true),
        new("projects", true), new("publications", false), new("volunteering", true),
    ];

    // Legal experienced: Bar Admissions stay included (research calls the block "mandatory"
    // regardless of seniority) but no longer gate above Experience once there's a track record.
    private static readonly IReadOnlyList<SectionConfigEntry> LegalExperiencedOrder =
    [
        new("experience", true), new("education", true), new("skills", true), new("projects", true),
        new("credentials", true), new("publications", false), new("volunteering", true),
    ];

    // Healthcare: Licenses & Certifications (credentials) gate above Experience — research
    // didn't call out a junior/experienced split for this industry, so one order for both.
    private static readonly IReadOnlyList<SectionConfigEntry> HealthcareOrder =
    [
        new("credentials", true), new("experience", true), new("education", true), new("skills", true),
        new("projects", true), new("publications", false), new("volunteering", true),
    ];

    // Trades/Hospitality: Skills, then Certifications & Licenses, both gate above Experience.
    private static readonly IReadOnlyList<SectionConfigEntry> TradesHospitalityOrder =
    [
        new("skills", true), new("credentials", true), new("experience", true), new("education", true),
        new("projects", true), new("publications", false), new("volunteering", true),
    ];

    public static readonly IReadOnlyList<ResumeIndustryTemplate> All =
    [
        new(Key: "tech", DisplayName: "Tech", HasSeniorityToggle: false, JuniorOrder: Baseline, ExperiencedOrder: Baseline),
        new(Key: "sales_retail", DisplayName: "Sales / Retail", HasSeniorityToggle: true, JuniorOrder: EducationGateOrder, ExperiencedOrder: Baseline),
        new(Key: "finance_consulting", DisplayName: "Finance / Consulting", HasSeniorityToggle: true, JuniorOrder: EducationGateOrder, ExperiencedOrder: Baseline),
        new(Key: "legal", DisplayName: "Legal", HasSeniorityToggle: true, JuniorOrder: LegalJuniorOrder, ExperiencedOrder: LegalExperiencedOrder),
        new(Key: "healthcare", DisplayName: "Healthcare", HasSeniorityToggle: false, JuniorOrder: HealthcareOrder, ExperiencedOrder: HealthcareOrder),
        new(Key: "trades_hospitality", DisplayName: "Trades / Hospitality", HasSeniorityToggle: false, JuniorOrder: TradesHospitalityOrder, ExperiencedOrder: TradesHospitalityOrder),
        // Portfolio link promotion (the one distinguishing signal research found for Creative) is
        // already handled by the existing Personal.PortfolioUrl / ResumeRenderer.ContactLine —
        // nothing else in the research specifies a section reorder, so this stays at baseline.
        new(Key: "creative", DisplayName: "Creative", HasSeniorityToggle: false, JuniorOrder: Baseline, ExperiencedOrder: Baseline),
        // Academia is flagged in the ticket's own research as a likely-distinct document mode
        // (CV, not resume) and explicitly out of scope this phase — default to the generic
        // baseline rather than inventing a Publications/Teaching/Grants gate order now.
        new(Key: "academia", DisplayName: "Academia", HasSeniorityToggle: false, JuniorOrder: Baseline, ExperiencedOrder: Baseline),
        // Same scope cut for US federal/USAJobs strict-mode handling (rigid required fields,
        // 2-page limit) — out of scope this phase, generic baseline until it's built for real.
        new(Key: "government_us", DisplayName: "Government (US federal)", HasSeniorityToggle: false, JuniorOrder: Baseline, ExperiencedOrder: Baseline),
    ];

    public static ResumeIndustryTemplate? Find(string key) =>
        All.FirstOrDefault(t => t.Key == key);
}
