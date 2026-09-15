using System.Globalization;
using System.Text;

namespace PostingEvaluatorBenchmark.Fixtures;

// Which of the two non-citizen/PR sponsorship checks in evaluate_posting.md's Step 1 apply to
// this candidate -- mirrors sponsorship.candidate_status.{citizen_or_permanent_resident,
// has_current_work_visa} in the real job_criteria.yaml schema exactly (see
// skills/context/job_criteria.yaml and evaluate_posting.md's "Sponsorship & citizenship/PR
// status" section).
public enum SponsorshipStance
{
    CitizenOrPr,       // citizen_or_permanent_resident: true -- both checks skipped.
    Unanswered,         // whole sponsorship section omitted -- behaves identically to CitizenOrPr.
    NonCitizenHasVisa,  // false / true -- only citizens_pr_only is checked.
    NonCitizenNoVisa,   // false / false -- both citizens_pr_only and sponsorship_not_offered are checked.
}

// One skill dimension as GroundTruthResolver.cs understands it. Every criteria profile carries
// these internally regardless of which YAML shape it renders (see UseLegacySkillDimensionsFormat
// below) -- this is the ground-truth author's own judgment of what "strong" vs "good" vs
// "excluded" means for this dimension, used by the resolver to grade the model's output
// deterministically. Terms are matched case-insensitively as exact posting-keyword membership
// (see PostingFixture.DimensionKeywords) -- never fuzzy/substring -- so grading never has to
// guess at what a keyword "counts as".
public record SkillDimensionSpec(string Name, string[] Strong, string[] Good, string[] Acceptable, string[] Excluded);

public record CriteriaProfile(
    string Id,
    string Description,
    string ProfessionFamily,           // "software" | "business_analyst" | "nursing" | "teaching" -- see PostingFixture.ProfessionFamily.
    string ProfessionCategoryLabel,    // YAML profession_category text.
    string TargetJobTitles,
    SponsorshipStance Sponsorship,
    string PreferredCountry,
    bool RemoteAccepted,
    bool HybridAccepted,
    double IdealMaxYears,
    double AcceptableMaxYears,          // AcceptableMinYears is always IdealMaxYears (contiguous ranges, matching the real fixture's convention).
    string SalaryCurrency,
    decimal TargetLow,
    decimal TargetHigh,
    decimal FlagBelow,
    decimal FlagAbove,
    SkillDimensionSpec[] Skills,        // Skills[0] is the primary/highest-priority dimension.
    string PrimaryDisqualifierId,       // hard-disqualifier id triggered when a posting's Dimension0Keywords hit Skills[0].Excluded.
    string[] CompanyPreferred,
    string[] CompanyAcceptable,
    string[] CompanyWeaker,
    string[] CompanyExcluded,
    string[] RoleTypePreferred,
    string[] RoleTypeAcceptable,
    string[] RoleTypeWeaker,
    string[] RoleTypeExcluded,
    bool IncludeGamblingDisqualifier,
    bool IncludeSoloEngineerDisqualifier,
    bool UseLegacySkillDimensionsFormat // true = render skill_dimensions: (rich per-name tiers, as evaluate_posting.md's "Legacy criteria format" section describes); false = render the current flat skills: list (names only).
)
{
    // Renders this profile into the real job_criteria.yaml shape PostingEvaluator's
    // BuildSystemPrompt interpolates verbatim as UserProfile.JobCriteria -- see
    // skills/context/job_criteria.yaml and job_criteria_templates/*.yaml for the schema this
    // mirrors. Only sections evaluate_posting.md actually reads are included.
    public string ToJobCriteriaYaml()
    {
        var sb = new StringBuilder();
        sb.AppendLine("version: \"2.0\"");
        sb.AppendLine($"profession_category: \"{ProfessionCategoryLabel}\"");
        sb.AppendLine($"target_job_titles: \"{TargetJobTitles}\"");
        sb.AppendLine("employment_type_preference: [full_time]");
        sb.AppendLine();

        sb.AppendLine("hard_disqualifiers:");
        if (Skills.Length > 0 && Skills[0].Excluded.Length > 0)
        {
            sb.AppendLine($"  - id: {PrimaryDisqualifierId}");
            sb.AppendLine($"    description: Primary {Skills[0].Name} is one of the excluded options below.");
            sb.AppendLine($"    signals: [{string.Join(", ", Skills[0].Excluded)}]");
        }
        if (IncludeGamblingDisqualifier)
        {
            sb.AppendLine("  - id: gambling_core");
            sb.AppendLine("    description: Company operates in gambling, betting, or related sectors");
            sb.AppendLine("    signals: [gambling, sports betting, \"online casino\", wagering, iGaming, sportsbook]");
        }
        if (IncludeSoloEngineerDisqualifier)
        {
            sb.AppendLine("  - id: solo_engineer");
            sb.AppendLine("    description: Candidate would be the only engineer/specialist in the company");
            sb.AppendLine("    signals: [\"first engineer\", \"you will be our only developer\", \"build the function from scratch\"]");
        }
        sb.AppendLine();

        sb.AppendLine("location:");
        sb.AppendLine("  preferred:");
        sb.AppendLine($"    - country: {PreferredCountry}");
        sb.AppendLine($"  remote:");
        sb.AppendLine($"    accepted: {(RemoteAccepted ? "true" : "false")}");
        sb.AppendLine($"  hybrid:");
        sb.AppendLine($"    accepted: {(HybridAccepted ? "true" : "false")}");
        sb.AppendLine();

        sb.AppendLine("sponsorship:");
        sb.AppendLine("  candidate_status:");
        switch (Sponsorship)
        {
            case SponsorshipStance.CitizenOrPr:
                sb.AppendLine("    citizen_or_permanent_resident: true");
                break;
            case SponsorshipStance.Unanswered:
                sb.AppendLine("    # citizen_or_permanent_resident: unanswered -- omitted, same as true.");
                break;
            case SponsorshipStance.NonCitizenHasVisa:
                sb.AppendLine("    citizen_or_permanent_resident: false");
                sb.AppendLine("    has_current_work_visa: true");
                break;
            case SponsorshipStance.NonCitizenNoVisa:
                sb.AppendLine("    citizen_or_permanent_resident: false");
                sb.AppendLine("    has_current_work_visa: false");
                break;
        }
        sb.AppendLine();

        sb.AppendLine("experience:");
        sb.AppendLine("  ranges:");
        sb.AppendLine($"    ideal:");
        sb.AppendLine($"      max_required: {Fmt(IdealMaxYears)} years");
        sb.AppendLine($"    acceptable:");
        sb.AppendLine($"      min_required: {Fmt(IdealMaxYears)} years");
        sb.AppendLine($"      max_required: {Fmt(AcceptableMaxYears)} years");
        sb.AppendLine($"    excluded:");
        sb.AppendLine($"      min_required: {Fmt(AcceptableMaxYears)} years");
        sb.AppendLine();

        sb.AppendLine("salary:");
        sb.AppendLine($"  currency: {SalaryCurrency}");
        sb.AppendLine("  thresholds:");
        sb.AppendLine($"    flag_below: {FmtMoney(FlagBelow)}");
        sb.AppendLine($"    target_range: [{FmtMoney(TargetLow)}, {FmtMoney(TargetHigh)}]");
        sb.AppendLine($"    flag_above: {FmtMoney(FlagAbove)}");
        sb.AppendLine();

        if (UseLegacySkillDimensionsFormat)
        {
            sb.AppendLine("skill_dimensions:");
            for (int i = 0; i < Skills.Length; i++)
            {
                var s = Skills[i];
                sb.AppendLine($"  - name: \"{s.Name}\"");
                sb.AppendLine($"    priority: {i + 1}");
                if (s.Strong.Length > 0) sb.AppendLine($"    strong_match: [{string.Join(", ", s.Strong)}]");
                if (s.Good.Length > 0) sb.AppendLine($"    good_match: [{string.Join(", ", s.Good)}]");
                if (s.Acceptable.Length > 0) sb.AppendLine($"    acceptable: [{string.Join(", ", s.Acceptable)}]");
                if (s.Excluded.Length > 0) sb.AppendLine($"    excluded: [{string.Join(", ", s.Excluded)}]");
            }
        }
        else
        {
            sb.AppendLine($"skills: [{string.Join(", ", Skills.Select(s => $"\"{s.Name}\""))}]");
        }
        sb.AppendLine();

        sb.AppendLine("company:");
        AppendKeywordList(sb, "  preferred:", CompanyPreferred);
        AppendKeywordList(sb, "  acceptable:", CompanyAcceptable);
        AppendKeywordList(sb, "  weaker:", CompanyWeaker);
        AppendKeywordList(sb, "  excluded:", CompanyExcluded);
        sb.AppendLine();

        sb.AppendLine("role_type:");
        AppendKeywordList(sb, "  preferred:", RoleTypePreferred);
        AppendKeywordList(sb, "  acceptable:", RoleTypeAcceptable);
        AppendKeywordList(sb, "  weaker:", RoleTypeWeaker);
        AppendKeywordList(sb, "  excluded:", RoleTypeExcluded);

        return sb.ToString();
    }

    private static void AppendKeywordList(StringBuilder sb, string header, string[] items)
    {
        if (items.Length == 0) return;
        sb.AppendLine(header);
        foreach (var item in items) sb.AppendLine($"    - {item}");
    }

    private static string Fmt(double years) =>
        years == Math.Floor(years) ? ((int)years).ToString(CultureInfo.InvariantCulture) : years.ToString("0.#", CultureInfo.InvariantCulture);

    private static string FmtMoney(decimal amount) => amount.ToString("0", CultureInfo.InvariantCulture);
}
