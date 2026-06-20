using System.Text;

namespace JobSearch.Data;

public static class EvalFormatter
{
    public static string Format(PostingEvaluation ev, string? via = null)
    {
        var rec = ev.Recommendation switch
        {
            "strong_match" => "STRONG MATCH",
            "good_match"   => "GOOD MATCH",
            "weak_match"   => "WEAK MATCH",
            "discard"      => "DISCARD",
            _              => ev.Recommendation.ToUpperInvariant(),
        };

        var sb = new StringBuilder();
        sb.AppendLine($"<b>{ev.Company} — {ev.RoleTitle}</b>");
        sb.AppendLine(via is not null
            ? $"<b>{rec}</b> ({via})"
            : $"<b>Recommendation: {rec}</b>");

        if (ev.DisqualifierHit is not null)
            sb.AppendLine($"Disqualifier: {ev.DisqualifierHit}");

        sb.AppendLine();
        sb.AppendLine("<b>Dimensions:</b>");
        sb.AppendLine($"Sponsorship: {ev.SponsorshipVerdict}{(ev.SponsorshipEvidence is not null ? $" ({ev.SponsorshipEvidence})" : "")}");
        sb.AppendLine($"Location: {ev.LocationDetail} ({ev.LocationMatch})");
        sb.AppendLine($"Experience: {ev.ExperienceDetail} ({ev.ExperienceMatch})");

        var backend = ev.BackendTechnologies.Length > 0
            ? string.Join(", ", ev.BackendTechnologies) : "not stated";
        sb.AppendLine($"Backend: {backend} ({ev.BackendMatch})");

        var frontend = ev.FrontendTechnologies.Length > 0
            ? string.Join(", ", ev.FrontendTechnologies) : "not stated";
        sb.AppendLine($"Frontend: {frontend} ({ev.FrontendMatch})");

        sb.AppendLine($"Salary: {ev.SalaryDetail ?? "not stated"} ({ev.SalaryAssessment})");
        sb.AppendLine($"Company: {ev.CompanyAssessment}");
        sb.AppendLine($"Role type: {ev.RoleTypeMatch}");

        sb.AppendLine();
        if (ev.OrangeFlags.Length > 0)
        {
            sb.AppendLine("<b>Orange flags:</b>");
            foreach (var flag in ev.OrangeFlags)
                sb.AppendLine($"• {flag}");
        }
        else
        {
            sb.AppendLine("<b>Orange flags:</b> none");
        }

        sb.AppendLine();
        sb.AppendLine($"<b>Rationale:</b> {ev.Rationale}");

        if (ev.SourceUrl is not null)
            sb.Append($"\n<a href=\"{ev.SourceUrl}\">View posting</a>");

        return sb.ToString().TrimEnd();
    }
}
