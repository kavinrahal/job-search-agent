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

        foreach (var skill in ev.SkillMatches)
            sb.AppendLine($"{skill.Dimension}: {(skill.Detail.Length > 0 ? skill.Detail : "not stated")} ({skill.Match})");

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
            sb.Append($"\n{ev.SourceUrl}");

        return sb.ToString().TrimEnd();
    }

    // Reuses Format's HTML body for email — SendGridEmailService only sends text/plain, and
    // the only HTML Format actually uses is <b> for emphasis, so stripping those tags gives
    // a clean plain-text body without duplicating the whole layout a second time.
    public static (string Subject, string Body) FormatPlainTextEmail(PostingEvaluation ev, string? via = null)
    {
        var rec = ev.Recommendation switch
        {
            "strong_match" => "STRONG MATCH",
            "good_match"   => "GOOD MATCH",
            "weak_match"   => "WEAK MATCH",
            "discard"      => "DISCARD",
            _              => ev.Recommendation.ToUpperInvariant(),
        };
        var subject = $"{rec}: {ev.RoleTitle} at {ev.Company}";
        var body = Format(ev, via).Replace("<b>", "").Replace("</b>", "");
        return (subject, body);
    }

    // Synthesizes a minimal posting description from stored evaluation fields.
    // Used when the original job page can't be re-fetched (bot protection, DNS, etc.).
    public static string ToPostingContext(PostingEvaluation ev)
    {
        var skills = ev.SkillMatches.Length > 0
            ? string.Join("\n", ev.SkillMatches.Select(s => $"{s.Dimension}: {(s.Detail.Length > 0 ? s.Detail : "not stated")}"))
            : "not stated";
        return $"""
        Company: {ev.Company}
        Role: {ev.RoleTitle}
        Source URL: {ev.SourceUrl ?? "not available"}
        {skills}
        Location: {ev.LocationDetail}
        Salary: {ev.SalaryDetail ?? "not stated"}
        Experience required: {ev.ExperienceDetail}
        Company assessment: {ev.CompanyAssessment}
        Role type: {ev.RoleTypeMatch}

        [Full posting text unavailable — generate from the structured data above and the evaluation JSON.]
        """;
    }
}
