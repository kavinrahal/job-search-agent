using System.Text.Json;
using JobSearch.Data;

namespace JobSearchAgent.Tests;

// Extraction-only tests, same principle as ResumeIntakeAgentTests — verifies the tool-response
// parsing logic without a live API call, using JsonDocument to build inputs shaped exactly like
// what Anthropic's tool_use.input would contain. Covers ResumeOverrideSchema, shared by
// ResumeBackfillAgent (Deploy A, one-time migration) and CvTailorAgent (Deploy B, per-application
// tailoring) — both populate the same shapes via different judgment rules.
public class ResumeOverrideSchemaTests
{
    private static IReadOnlyDictionary<string, JsonElement> Input(string json)
    {
        var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
    }

    [Fact]
    public void ExtractSectionConfig_ReturnsEntriesInOrder()
    {
        var input = Input("""{"section_config": [{"section_key": "experience", "included": true}, {"section_key": "skills", "included": false}]}""");

        var result = ResumeOverrideSchema.ExtractSectionConfig(input, "section_config");

        Assert.Equal(2, result.Count);
        Assert.Equal(new SectionConfigEntry("experience", true), result[0]);
        Assert.Equal(new SectionConfigEntry("skills", false), result[1]);
    }

    [Fact]
    public void ExtractSectionConfig_MissingKey_ReturnsEmpty()
    {
        var result = ResumeOverrideSchema.ExtractSectionConfig(Input("{}"), "section_config");

        Assert.Empty(result);
    }

    [Fact]
    public void ExtractExperienceOverrides_CapturesTextOverrideExtraAchievementsAndNotes()
    {
        var input = Input("""
            {
              "experience_overrides": [
                {
                  "experience_index": 0,
                  "included": true,
                  "company_description_override": "Shorter description.",
                  "achievements": [{"index": 0, "included": true, "text_override": "Reworded bullet."}],
                  "extra_achievements": ["Synthesized bullet with no source."],
                  "notes": "De-emphasise in some applications."
                },
                {
                  "experience_index": 3,
                  "included": false,
                  "achievements": [],
                  "extra_achievements": [],
                  "notes": "Weakest role — only include for design-focused applications."
                }
              ]
            }
            """);

        var result = ResumeOverrideSchema.ExtractExperienceOverrides(input, "experience_overrides");

        Assert.Equal(2, result.Count);
        Assert.Equal(0, result[0].ExperienceIndex);
        Assert.True(result[0].Included);
        Assert.Equal("Shorter description.", result[0].CompanyDescriptionOverride);
        Assert.Single(result[0].Achievements);
        Assert.Equal("Reworded bullet.", result[0].Achievements[0].TextOverride);
        Assert.Single(result[0].ExtraAchievements);
        Assert.Equal("Synthesized bullet with no source.", result[0].ExtraAchievements[0]);

        Assert.Equal(3, result[1].ExperienceIndex);
        Assert.False(result[1].Included);
        Assert.Contains("Weakest role", result[1].Notes);
    }

    [Fact]
    public void ExtractItemOverrides_ParsesOrderWhenPresent_NullWhenOmitted()
    {
        var input = Input("""
            {
              "experience_overrides": [
                {
                  "experience_index": 0,
                  "included": true,
                  "achievements": [
                    {"index": 2, "included": true, "order": 0},
                    {"index": 0, "included": true}
                  ],
                  "extra_achievements": []
                }
              ]
            }
            """);

        var result = ResumeOverrideSchema.ExtractExperienceOverrides(input, "experience_overrides");

        var achievements = result[0].Achievements;
        Assert.Equal(0, achievements[0].Order);
        Assert.Null(achievements[1].Order);
    }

    [Fact]
    public void ExtractSkillsSection_TranscribesLabelsAndItemsVerbatim()
    {
        var input = Input("""
            {"skills_section": [
              {"label": "Languages", "items": ["C#", "TypeScript"]},
              {"label": "Cloud", "items": ["Azure (App Services, Functions)", "AWS"]}
            ]}
            """);

        var result = ResumeOverrideSchema.ExtractSkillsSection(input, "skills_section");

        Assert.Equal(2, result.Count);
        Assert.Equal("Languages", result[0].Label);
        Assert.Equal(["C#", "TypeScript"], result[0].Items);
        Assert.Equal("Azure (App Services, Functions)", result[1].Items[0]);
    }

    [Fact]
    public void ExtractProjectOverrides_CapturesHighlightOverridesAndExtras()
    {
        var input = Input("""
            {
              "project_overrides": [
                {
                  "project_index": 0,
                  "included": true,
                  "description_override": "Short tailored description.",
                  "highlights": [{"index": 1, "included": false}],
                  "extra_highlights": ["Highlight with no Background source."]
                }
              ]
            }
            """);

        var result = ResumeOverrideSchema.ExtractProjectOverrides(input, "project_overrides");

        var project = Assert.Single(result);
        Assert.Equal("Short tailored description.", project.DescriptionOverride);
        Assert.Single(project.Highlights);
        Assert.False(project.Highlights[0].Included);
        Assert.Single(project.ExtraHighlights);
    }

    [Fact]
    public void ExtractExperienceOverrides_IncludedDefaultsTrue_WhenFieldOmitted()
    {
        // GetBool's defaultValue: true for "included" matters here — an override entry that
        // forgets to set included shouldn't silently exclude a real, currently-live role.
        var input = Input("""
            {"experience_overrides": [{"experience_index": 0, "achievements": [], "extra_achievements": []}]}
            """);

        var result = ResumeOverrideSchema.ExtractExperienceOverrides(input, "experience_overrides");

        Assert.True(Assert.Single(result).Included);
    }

    // Regression coverage for the CV-tailoring hardening finding: extra_achievements/
    // extra_highlights used to carry one hardcoded schema description ("Bullets for this role
    // with no BACKGROUND source") shared by both callers, which was accurate for
    // ResumeBackfillAgent (transcribing a real CV_BASE document) but read as "invent freely" when
    // reused verbatim for CvTailorAgent's per-application calls, contradicting tailor_cv.md's own
    // "do not fabricate" rule. The fix makes the description caller-supplied instead of hardcoded
    // — these tests confirm the schema builder actually plumbs it through rather than ignoring it.
    [Fact]
    public void PropExperienceOverrideArray_EmbedsCallerSuppliedExtraAchievementsDescription()
    {
        var schema = ResumeOverrideSchema.PropExperienceOverrideArray("achievements rule", "custom extra-achievements grounding rule");

        var description = schema.GetProperty("items").GetProperty("properties")
            .GetProperty("extra_achievements").GetProperty("description").GetString();

        Assert.Equal("custom extra-achievements grounding rule", description);
    }

    [Fact]
    public void PropProjectOverrideArray_EmbedsCallerSuppliedExtraHighlightsDescription()
    {
        var schema = ResumeOverrideSchema.PropProjectOverrideArray("highlights rule", "custom extra-highlights grounding rule");

        var description = schema.GetProperty("items").GetProperty("properties")
            .GetProperty("extra_highlights").GetProperty("description").GetString();

        Assert.Equal("custom extra-highlights grounding rule", description);
    }
}
