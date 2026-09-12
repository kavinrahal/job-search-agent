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

    // BuildSystemPrompt's includeContactInfo behavior — the actual fix for "CvTailorAgent sends
    // the candidate's contact info into the CV-tailoring prompt unnecessarily". internal (not
    // private) specifically so this is assertable without a live API call, same rationale as
    // ApplyDeltaAndRender's own split-out above. Constructs a real CvTailorAgent with a dummy key:
    // the constructor only loads local skill text and stores the key on an AnthropicClient, no
    // network call, so no live credential is needed to test prompt construction.
    private static readonly BackgroundData ContactBackground = new()
    {
        Personal = new PersonalInfo
        {
            Name = "Jordan Rivers", Email = "jordan.rivers@example.com", Phone = "555-0199",
            Location = "Springfield, IL", Linkedin = "linkedin.com/in/jordanrivers", Github = "github.com/jordanrivers",
        },
        Experience = [new ExperienceEntry { Company = "Acme", Role = "Engineer", Achievements = ["Did A."] }],
    };

    private const string ContactBackgroundYaml = """
        personal:
          name: Jordan Rivers
          email: jordan.rivers@example.com
          phone: "555-0199"
          location: Springfield, IL
          linkedin: linkedin.com/in/jordanrivers
          github: github.com/jordanrivers
        experience:
          - company: Acme
            role: Engineer
        """;

    [Fact]
    public void BuildSystemPrompt_IncludeContactInfoFalse_NeverContainsContactFields()
    {
        var agent = new CvTailorAgent("test-key");

        var prompt = agent.BuildSystemPrompt(ContactBackground, ContactBackgroundYaml, BaseResume(), includeContactInfo: false);

        Assert.DoesNotContain("Jordan Rivers", prompt);
        Assert.DoesNotContain("jordan.rivers@example.com", prompt);
        Assert.DoesNotContain("555-0199", prompt);
        Assert.DoesNotContain("Springfield, IL", prompt);
        Assert.DoesNotContain("linkedin.com/in/jordanrivers", prompt);
        Assert.DoesNotContain("github.com/jordanrivers", prompt);
        // The content tailoring actually needs still made it into the prompt.
        Assert.Contains("Acme", prompt);
    }

    // BuildRevisionSystemPrompt backs ReviseAsync — the fix for the CV-revision-flow finding
    // ("ReviseAsync still lets contact info flow through to Claude"). Unlike GenerateAsync's
    // BuildSystemPrompt(includeContactInfo: false), the name must survive (needed to keep
    // personalizing the header the model reproduces), but email/phone/location/linkedin/github
    // must not reach Claude any more than they do for first-generation tailoring.
    [Fact]
    public void BuildRevisionSystemPrompt_NeverContainsContactFieldsButKeepsName()
    {
        var agent = new CvTailorAgent("test-key");

        var prompt = agent.BuildRevisionSystemPrompt(ContactBackground, ContactBackgroundYaml, BaseResume());

        Assert.DoesNotContain("jordan.rivers@example.com", prompt);
        Assert.DoesNotContain("555-0199", prompt);
        Assert.DoesNotContain("Springfield, IL", prompt);
        Assert.DoesNotContain("linkedin.com/in/jordanrivers", prompt);
        Assert.DoesNotContain("github.com/jordanrivers", prompt);
        // Name still appears (needed for the header/personalization) and content still flows.
        Assert.Contains("Jordan Rivers", prompt);
        Assert.Contains("Acme", prompt);
    }

    [Fact]
    public void WithRedactedContact_KeepsNameAndContentBlanksOtherContactFields()
    {
        var redacted = CvTailorAgent.WithRedactedContact(ContactBackground);

        Assert.Equal("Jordan Rivers", redacted.Personal.Name);
        Assert.Equal("", redacted.Personal.Email);
        Assert.Equal("", redacted.Personal.Phone);
        Assert.Equal("", redacted.Personal.Location);
        Assert.Equal("", redacted.Personal.Linkedin);
        Assert.Equal("", redacted.Personal.Github);
        Assert.Same(ContactBackground.Experience, redacted.Experience);
    }

    // RestoreContactLine backs ReviseAsync's post-processing step: since the model never sees the
    // candidate's real contact fields (BuildRevisionSystemPrompt), whatever it reproduces in that
    // region of its raw output is replaced wholesale with the real, deterministically-rendered
    // contact line before the text is persisted as the final resume.
    [Fact]
    public void RestoreContactLine_BlankContactRegion_InsertsRealContactLine()
    {
        var revised = "# Jordan Rivers\n\n\n\n## Summary\n\nA tailored summary.\n";

        var restored = CvTailorAgent.RestoreContactLine(revised, ContactBackground.Personal);

        Assert.Contains("jordan.rivers@example.com | 555-0199 | Springfield, IL | linkedin.com/in/jordanrivers | github.com/jordanrivers", restored);
        Assert.Contains("## Summary", restored);
        Assert.Contains("A tailored summary.", restored);
        Assert.StartsWith("# Jordan Rivers", restored);
    }

    [Fact]
    public void RestoreContactLine_ModelEchoedSomethingInContactRegion_StillReplacedWithRealLine()
    {
        // Even if the model reproduces something other than blank in the contact region (it was
        // never shown the real value, so anything there is not the candidate's real contact info),
        // the real line still wins.
        var revised = "# Jordan Rivers\n\ncontact info unavailable\n\n## Summary\n\nSummary text.\n";

        var restored = CvTailorAgent.RestoreContactLine(revised, ContactBackground.Personal);

        Assert.DoesNotContain("contact info unavailable", restored);
        Assert.Contains("jordan.rivers@example.com", restored);
    }

    [Fact]
    public void RestoreContactLine_NotWellFormed_ReturnsUnchanged()
    {
        var malformed = "Sorry, I can't help with that.";

        var restored = CvTailorAgent.RestoreContactLine(malformed, ContactBackground.Personal);

        Assert.Equal(malformed, restored);
    }

    // Regression test for the CV-tailoring hardening finding: extra_achievements/extra_highlights
    // was a labeled exception with no requirement that its content trace back to real source
    // material — its schema description used to just say "Bullets for this role with no
    // BACKGROUND source", identical to (and copied from) ResumeBackfillAgent's one-time
    // transcription-of-a-real-document use case, which read as unconditional license to invent
    // when reused for per-application tailoring. This asserts the actual production string
    // (wired into both submit_experience_overrides and submit_project_overrides via
    // ResumeOverrideSchema) now requires the bullet already be real (verbatim in CURRENT RESUME)
    // and explicitly forbids invention, rather than being silent on sourcing.
    [Fact]
    public void ExtraAchievementsNote_RequiresExistingCurrentResumeContent_ForbidsInvention()
    {
        Assert.Contains("Never invent", CvTailorAgent.ExtraAchievementsNote);
        Assert.Contains("CURRENT RESUME", CvTailorAgent.ExtraAchievementsNote);
        Assert.DoesNotContain("no BACKGROUND source.", CvTailorAgent.ExtraAchievementsNote);
    }
}
