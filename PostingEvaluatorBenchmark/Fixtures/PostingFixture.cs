using System.Text;

namespace PostingEvaluatorBenchmark.Fixtures;

public enum SponsorshipLine
{
    None,                 // posting says nothing about visa/citizenship.
    CitizensOrPrOnly,      // explicit citizens/PR-only language.
    NoSponsorshipOffered,  // explicit "no sponsorship" language.
    Both,                  // both phrases present.
}

public enum TeamSizeLine { Unstated, SoloEngineer, SmallTeam, LargeTeam }

public enum ArrangementLine { Unstated, Remote, Hybrid, OnSite }

// A synthetic job posting with every fact GroundTruthResolver needs baked in as structured data
// -- see that class for how these fields become the objective expected outcome for any
// (CriteriaProfile, PostingFixture) pairing. Render() turns the same structured facts into
// realistic posting prose, so the model only ever sees text, never the structured ground truth.
public record PostingFixture(
    string Id,
    string Description,
    string Company,
    string RoleTitle,
    string ProfessionFamily,       // must equal a CriteriaProfile.ProfessionFamily value for a "same discipline" pairing.
    SponsorshipLine Sponsorship,
    string? Country,                // null = location entirely unstated (paired with Arrangement.Unstated).
    ArrangementLine Arrangement,
    string? CityOrRegion,
    double? YearsMidpoint,           // null = experience requirement entirely unstated.
    string? YearsRawText,            // e.g. "3-6 years" -- what the posting literally says (midpoint already computed above).
    decimal? SalaryMidpoint,         // null = salary entirely unstated.
    string SalaryCurrency,
    string? SalaryRawText,
    bool IsGamblingIndustry,
    TeamSizeLine TeamSize,
    string[] Dimension0Keywords,     // terms describing the primary skill dimension; [] = not addressed ("missing").
    string[] Dimension1Keywords,
    string[] Dimension2Keywords,
    string[] CompanyKeywords,
    string[] RoleTypeKeywords,
    string CompanyDescriptionText,
    string RoleDescriptionText
)
{
    public string Render()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Company: {Company}");
        sb.AppendLine($"Role: {RoleTitle}");

        if (Country is null && Arrangement == ArrangementLine.Unstated)
        {
            // Deliberately silent on location -- exercises location_match: "missing".
        }
        else
        {
            string arrangement = Arrangement switch
            {
                ArrangementLine.Remote => "remote",
                ArrangementLine.Hybrid => "hybrid",
                ArrangementLine.OnSite => "on-site",
                _ => "",
            };
            string place = CityOrRegion ?? Country ?? "";
            sb.AppendLine($"Location: {place}{(arrangement.Length > 0 ? $" ({arrangement})" : "")}");
        }

        if (SalaryRawText is not null)
            sb.AppendLine($"Salary: {SalaryRawText}");

        if (YearsRawText is not null)
            sb.AppendLine($"Experience: {YearsRawText}");

        sb.AppendLine($"Description: {RoleDescriptionText}");

        var stackTerms = Dimension0Keywords.Concat(Dimension1Keywords).Concat(Dimension2Keywords).ToArray();
        if (stackTerms.Length > 0)
            sb.AppendLine($"Stack: {string.Join(", ", stackTerms)}");

        if (CompanyDescriptionText.Length > 0)
            sb.AppendLine($"About us: {CompanyDescriptionText}");

        var notes = new List<string>();
        switch (Sponsorship)
        {
            case SponsorshipLine.CitizensOrPrOnly:
                notes.Add("Applicants must be citizens or permanent residents.");
                break;
            case SponsorshipLine.NoSponsorshipOffered:
                notes.Add("We are unable to offer visa sponsorship for this role.");
                break;
            case SponsorshipLine.Both:
                notes.Add("Applicants must be citizens or permanent residents. We are unable to offer visa sponsorship.");
                break;
        }
        switch (TeamSize)
        {
            case TeamSizeLine.SoloEngineer:
                notes.Add("You will be our first engineer, building the function from scratch.");
                break;
            case TeamSizeLine.SmallTeam:
                notes.Add("You'll join a close-knit team of 4 engineers.");
                break;
            case TeamSizeLine.LargeTeam:
                notes.Add("You'll join an established team of 30+ engineers across several squads.");
                break;
        }
        if (RoleTypeKeywords.Length > 0)
            notes.Add(string.Join(" ", RoleTypeKeywords));

        foreach (var note in notes) sb.AppendLine(note);

        return sb.ToString().TrimEnd();
    }
}
