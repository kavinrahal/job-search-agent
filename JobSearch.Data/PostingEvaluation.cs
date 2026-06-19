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
    public string BackendMatch { get; init; } = "";
    public string[] BackendTechnologies { get; init; } = [];
    public string FrontendMatch { get; init; } = "";
    public string[] FrontendTechnologies { get; init; } = [];
    public string SalaryAssessment { get; init; } = "";
    public string? SalaryDetail { get; init; }
    public string CompanyAssessment { get; init; } = "";
    public string RoleTypeMatch { get; init; } = "";
    public string[] OrangeFlags { get; init; } = [];
    public string Rationale { get; init; } = "";
}
