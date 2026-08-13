namespace JobSearch.Data;

public class PostingEvaluation
{
    public string Company { get; init; } = "";
    public string RoleTitle { get; init; } = "";
    public string? SourceUrl { get; init; }
    public string Recommendation { get; init; } = "";
    public string? DisqualifierHit { get; init; }
    public string SponsorshipVerdict { get; init; } = "";
    public string? SponsorshipEvidence { get; init; }
    public string LocationMatch { get; init; } = "";
    public string LocationDetail { get; init; } = "";
    public string ExperienceMatch { get; init; } = "";
    public string ExperienceDetail { get; init; } = "";
    // One entry per skill dimension defined in the candidate's own job criteria — e.g. a
    // software engineer's criteria might define "Backend stack" and "Frontend stack"; a
    // teacher's might define "Age group specialization". Generic across every profession
    // instead of hardcoding engineering-specific fields here.
    public SkillMatch[] SkillMatches { get; init; } = [];
    public string SalaryAssessment { get; init; } = "";
    public string? SalaryDetail { get; init; }
    public string CompanyAssessment { get; init; } = "";
    public string RoleTypeMatch { get; init; } = "";
    public string[] OrangeFlags { get; init; } = [];
    public string Rationale { get; init; } = "";
}

// Dimension is the skill's name as defined in the candidate's job criteria (e.g. "Backend
// stack", "Clinical specialty"), Match is a fit tier (strong|good|acceptable|excluded),
// Detail names the specific technologies/qualifications/etc. found in the posting.
public record SkillMatch(string Dimension, string Match, string Detail);
