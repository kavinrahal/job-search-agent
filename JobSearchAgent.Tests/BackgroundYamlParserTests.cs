using JobSearch.Data;

namespace JobSearchAgent.Tests;

public class BackgroundYamlParserTests
{
    // Uses the real skills/context/background.yaml fixture (same file TestFixtures.OwnerProfile
    // already loads) rather than a synthetic snippet — this is the actual document the resume
    // builder Phase 1 renderer/backfill need to parse correctly, irregular sections and all.
    private static readonly string RealYaml = SkillLoader.Load("context/background.yaml");

    [Fact]
    public void Parse_RealFixture_ExtractsPersonalInfo()
    {
        var data = BackgroundYamlParser.Parse(RealYaml);

        Assert.Equal("Kavin Abeysinghe", data.Personal.Name);
        Assert.Equal("kavinrahal@gmail.com", data.Personal.Email);
        Assert.Equal("linkedin.com/in/kavinrahal", data.Personal.Linkedin);
    }

    [Fact]
    public void Parse_RealFixture_ExtractsAllFourExperienceEntriesInOrder()
    {
        var data = BackgroundYamlParser.Parse(RealYaml);

        Assert.Equal(4, data.Experience.Count);
        Assert.Equal(["Willow Inc.", "Programmed", "Kolmeo Pty. Ltd.", "Epic Lanka Technologies"],
            data.Experience.Select(e => e.Company).ToList());
    }

    [Fact]
    public void Parse_RealFixture_ExtractsAchievementsAndStackForFirstRole()
    {
        var data = BackgroundYamlParser.Parse(RealYaml);
        var willow = data.Experience[0];

        Assert.Equal(7, willow.Achievements.Count);
        Assert.Contains("App Status", willow.Achievements[0]);
        Assert.Equal(["C#", "ASP.NET Core", "Azure", "Azure Data Explorer", "Azure Digital Twins"], willow.Stack["backend"]);
        Assert.Equal("2025-04", willow.Dates.Start);
        Assert.Equal("2026-05", willow.Dates.End);
    }

    [Fact]
    public void Parse_RealFixture_ExtractsEducationAndGpaNotes()
    {
        var data = BackgroundYamlParser.Parse(RealYaml);

        var edu = Assert.Single(data.Education);
        Assert.Equal("Royal Melbourne Institute of Technology (RMIT University)", edu.Institution);
        Assert.Equal(2022, edu.GraduationYear);
        Assert.Equal(2.9, edu.Gpa);
        Assert.NotNull(edu.GpaNotes);
    }

    [Fact]
    public void Parse_RealFixture_ExtractsNarrativeGuidance()
    {
        var data = BackgroundYamlParser.Parse(RealYaml);

        Assert.NotNull(data.Narrative);
        Assert.Equal(4, data.Narrative!.StrongestAnchorsInOrder.Count);
        Assert.Single(data.Narrative.GapToAddress);
        Assert.Contains("GPA (2.9", data.Narrative.DoNotLeadWith[0]);
        Assert.NotNull(data.Narrative.LayoffsContext);
    }

    [Fact]
    public void Parse_RealFixture_NewOptionalSectionsParseCorrectly()
    {
        // The real fixture now carries the Phase 1 schema extension: real Volunteering content
        // migrated in from cv_base.md (previously had no Background representation at all),
        // and explicitly empty Credentials/Publications for this candidate (a software engineer
        // with neither) — both must parse cleanly, not throw, and PortfolioUrl stays absent
        // since this candidate doesn't have one.
        var data = BackgroundYamlParser.Parse(RealYaml);

        Assert.Empty(data.Credentials);
        Assert.Empty(data.Publications);
        Assert.Equal(3, data.Volunteering.Count);
        Assert.Equal(["Mentor", "Student Mentor", "PR Manager"], data.Volunteering.Select(v => v.Role).ToList());
        Assert.Null(data.Personal.PortfolioUrl);
    }

    [Fact]
    public void Parse_RealFixture_MissingOptionalSections_ParsesToEmptyNotThrow()
    {
        // A user whose stored Background predates this schema extension entirely (no
        // credentials/publications/volunteering keys at all, not even as empty lists) must
        // still parse cleanly — every existing user's stored data looks like this today.
        var data = BackgroundYamlParser.Parse("personal:\n  name: Someone\n");

        Assert.Empty(data.Credentials);
        Assert.Empty(data.Publications);
        Assert.Empty(data.Volunteering);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsDefaultInsteadOfThrowing()
    {
        var data = BackgroundYamlParser.Parse("");

        Assert.Equal("", data.Personal.Name);
        Assert.Empty(data.Experience);
    }

    [Fact]
    public void Parse_NewOptionalSections_RoundTripWhenPresent()
    {
        const string yaml = """
            personal:
              name: Test Candidate
            credentials:
              - kind: license
                name: Registered Nurse
                issuer: Nursing Board
                status: Active
            publications:
              - title: A Paper
                venue: A Journal
            volunteering:
              - role: Mentor
                org: Some Org
                dates:
                  start: "2023-01"
            """;

        var data = BackgroundYamlParser.Parse(yaml);

        var credential = Assert.Single(data.Credentials);
        Assert.Equal("license", credential.Kind);
        Assert.Equal("Registered Nurse", credential.Name);

        var publication = Assert.Single(data.Publications);
        Assert.Equal("A Paper", publication.Title);

        var volunteering = Assert.Single(data.Volunteering);
        Assert.Equal("Mentor", volunteering.Role);
        Assert.Equal("2023-01", volunteering.Dates.Start);
    }
}
