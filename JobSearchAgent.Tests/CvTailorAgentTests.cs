using System.Text.Json;
using JobSearch.Data;

namespace JobSearchAgent.Tests;

// Tests CvTailorAgent.ApplyDeltaAndRender — the delta-combination logic split out of
// GenerateAsync specifically so it's testable without a live API call, same principle as
// ResumeIntakeAgent.ExtractField / ResumeOverrideSchema's extraction methods. Inputs are built
// with JsonDocument to match the shape of a real Anthropic tool_use.input.
public class CvTailorAgentTests
{
    private static IReadOnlyDictionary<string, JsonElement> Input(string json)
    {
        var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
    }

    private static BackgroundData Background() => new()
    {
        Personal = new PersonalInfo { Name = "Jordan Rivers" },
        Experience = [new ExperienceEntry { Company = "Acme", Role = "Engineer", Achievements = ["Did A.", "Did B."] }],
    };

    private static UserResume BaseResume() => new()
    {
        Summary = "Old summary.",
        SectionConfigJson = JsonSerializer.Serialize(new List<SectionConfigEntry> { new("experience", true) }),
        ExperienceOverridesJson = "[]",
        SkillsSectionJson = "[]",
        ProjectOverridesJson = "[]",
    };

    private static readonly IReadOnlyDictionary<string, JsonElement> EmptyExperience = Input("""{"experience_overrides": []}""");
    private static readonly IReadOnlyDictionary<string, JsonElement> EmptyProjects = Input("""{"project_overrides": []}""");

    [Fact]
    public void ApplyDeltaAndRender_UsesFreshSummary_NotBaseResumeSummary()
    {
        var summarySkills = Input("""{"summary": "Tailored summary for this posting.", "skills_section": []}""");

        var output = CvTailorAgent.ApplyDeltaAndRender(Background(), BaseResume(), summarySkills, EmptyExperience, EmptyProjects);

        Assert.Contains("Tailored summary for this posting.", output);
        Assert.DoesNotContain("Old summary.", output);
    }

    [Fact]
    public void ApplyDeltaAndRender_SectionConfigComesFromBaseResume_NotFromTheDelta()
    {
        // No tool call produces section_config for tailoring — this proves the base resume's
        // SectionConfigJson (which does include "experience") drives rendering, confirming
        // tailoring never touches which sections appear, only content within them.
        var summarySkills = Input("""{"summary": "S.", "skills_section": []}""");

        var output = CvTailorAgent.ApplyDeltaAndRender(Background(), BaseResume(), summarySkills, EmptyExperience, EmptyProjects);

        Assert.Contains("## Experience", output);
    }

    [Fact]
    public void ApplyDeltaAndRender_ExperienceOverride_AppliesRewordingAndOrder()
    {
        var summarySkills = Input("""{"summary": "S.", "skills_section": []}""");
        var experience = Input("""
            {"experience_overrides": [
              {"experience_index": 0, "included": true,
               "achievements": [{"index": 1, "included": true, "text_override": "Did B, rewritten.", "order": 0}],
               "extra_achievements": []}
            ]}
            """);

        var output = CvTailorAgent.ApplyDeltaAndRender(Background(), BaseResume(), summarySkills, experience, EmptyProjects);

        Assert.Contains("- Did B, rewritten.\n- Did A.", output);
    }

    [Fact]
    public void ApplyDeltaAndRender_SkillsSection_ReflectsReorderedDelta()
    {
        var summarySkills = Input("""
            {"summary": "S.", "skills_section": [{"label": "Cloud", "items": ["Azure"]}, {"label": "Languages", "items": ["C#"]}]}
            """);
        var resumeWithSkills = BaseResume();
        resumeWithSkills.SectionConfigJson = JsonSerializer.Serialize(new List<SectionConfigEntry> { new("skills", true) });

        var output = CvTailorAgent.ApplyDeltaAndRender(Background(), resumeWithSkills, summarySkills, EmptyExperience, EmptyProjects);

        Assert.Contains("**Cloud** – Azure\n**Languages** – C#", output);
    }

    [Fact]
    public void ApplyDeltaAndRender_ProjectOverride_ExcludesAndOverridesDescription()
    {
        var background = Background();
        background.Projects.Add(new ProjectEntry { Name = "Side Project", Description = "Raw description.", Highlights = ["H1", "H2"] });
        var resumeWithProjects = BaseResume();
        resumeWithProjects.SectionConfigJson = JsonSerializer.Serialize(new List<SectionConfigEntry> { new("projects", true) });

        var summarySkills = Input("""{"summary": "S.", "skills_section": []}""");
        var projects = Input("""
            {"project_overrides": [
              {"project_index": 0, "included": true, "description_override": "Tailored description.",
               "highlights": [{"index": 1, "included": false}], "extra_highlights": []}
            ]}
            """);

        var output = CvTailorAgent.ApplyDeltaAndRender(background, resumeWithProjects, summarySkills, EmptyExperience, projects);

        Assert.Contains("Tailored description.", output);
        Assert.Contains("- H1", output);
        Assert.DoesNotContain("H2", output);
    }
}
