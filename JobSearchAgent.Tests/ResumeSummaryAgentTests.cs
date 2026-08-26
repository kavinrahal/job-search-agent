using JobSearch.Data;

namespace JobSearchAgent.Tests;

// Tests ResumeSummaryAgent.IsBackgroundEssentiallyEmpty — the pure, testable-without-a-live-API
// guard POST /resume/generate-summary uses to reject a build-from-scratch user (just
// Name+Email, nothing else) before ever calling Claude. Split out for the same reason
// CvTailorAgent.ApplyDeltaAndRender is: exercisable without hitting the real API.
public class ResumeSummaryAgentTests
{
    private static readonly string RealYaml = SkillLoader.Load("context/background.yaml");

    [Fact]
    public void IsBackgroundEssentiallyEmpty_RealFixture_ReturnsFalse()
    {
        var data = BackgroundYamlParser.Parse(RealYaml);

        Assert.False(ResumeSummaryAgent.IsBackgroundEssentiallyEmpty(data));
    }

    [Fact]
    public void IsBackgroundEssentiallyEmpty_OnlyPersonalInfo_ReturnsTrue()
    {
        // A brand-new build-from-scratch user: just Name+Email, no experience/education/
        // projects/credentials/publications/volunteering yet.
        var data = BackgroundYamlParser.Parse("personal:\n  name: Someone\n  email: someone@example.com\n");

        Assert.True(ResumeSummaryAgent.IsBackgroundEssentiallyEmpty(data));
    }

    [Fact]
    public void IsBackgroundEssentiallyEmpty_EmptyBackground_ReturnsTrue()
    {
        var data = BackgroundYamlParser.Parse("");

        Assert.True(ResumeSummaryAgent.IsBackgroundEssentiallyEmpty(data));
    }

    [Theory]
    [InlineData("experience:\n  - company: Acme\n    role: Engineer\n")]
    [InlineData("education:\n  - institution: Some Uni\n    degree: BSc\n")]
    [InlineData("projects:\n  - name: Side Project\n")]
    [InlineData("credentials:\n  - kind: license\n    name: Registered Nurse\n")]
    [InlineData("publications:\n  - title: A Paper\n")]
    [InlineData("volunteering:\n  - role: Mentor\n    org: Some Org\n")]
    public void IsBackgroundEssentiallyEmpty_AnySingleSectionPopulated_ReturnsFalse(string yaml)
    {
        var data = BackgroundYamlParser.Parse(yaml);

        Assert.False(ResumeSummaryAgent.IsBackgroundEssentiallyEmpty(data));
    }
}
