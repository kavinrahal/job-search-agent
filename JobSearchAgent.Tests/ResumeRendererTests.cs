using System.Text.Json;
using JobSearch.Data;

namespace JobSearchAgent.Tests;

public class ResumeRendererTests
{
    private static UserResume EmptyResume() => new()
    {
        Summary = "",
        SectionConfigJson = "[]",
        ExperienceOverridesJson = "[]",
        SkillsSectionJson = "[]",
        ProjectOverridesJson = "[]",
    };

    private static BackgroundData MinimalBackground() => new()
    {
        Personal = new PersonalInfo { Name = "Jordan Rivers", Email = "jordan@example.com", Phone = "555-0100", Location = "Remote", Linkedin = "linkedin.com/in/jordan", Github = "github.com/jordan" },
    };

    [Fact]
    public void Render_HeaderAndContactLine_MatchCvBaseConvention()
    {
        var output = ResumeRenderer.Render(MinimalBackground(), EmptyResume());

        Assert.StartsWith("# Jordan Rivers\n\njordan@example.com | 555-0100 | Remote | linkedin.com/in/jordan | github.com/jordan\n", output);
    }

    // Display path (the resume builder preview, the saved/tailored resume) — a blank Summary
    // must never leak the LLM prompt instruction as if it were the user's actual content. See
    // the "Resume Builder preview leaks a raw LLM-prompt instruction" bug.
    [Fact]
    public void Render_BlankSummary_DisplayPath_OmitsSectionEntirely()
    {
        var output = ResumeRenderer.Render(MinimalBackground(), EmptyResume());

        Assert.DoesNotContain("Summary", output);
        Assert.DoesNotContain("[Fresh summary", output);
    }

    // Prompt-context path (CvTailorAgent's system prompt, and the accuracy-verifier source
    // material that deliberately mirrors it) — the instruction placeholder is genuinely useful
    // here, telling the model there's no current summary to preserve.
    [Fact]
    public void Render_BlankSummary_PromptContextPath_UsesInstructionPlaceholder()
    {
        var output = ResumeRenderer.Render(MinimalBackground(), EmptyResume(), isPromptContext: true);

        Assert.Contains("## Summary\n\n[Fresh summary specific to this role; see tailoring instructions]", output);
    }

    [Fact]
    public void Render_SetSummary_UsesStoredValueNotPlaceholder()
    {
        var resume = EmptyResume();
        resume.Summary = "Backend engineer with 4 years building payments systems.";

        var output = ResumeRenderer.Render(MinimalBackground(), resume);

        Assert.Contains("## Summary\n\nBackend engineer with 4 years building payments systems.", output);
        Assert.DoesNotContain("[Fresh summary", output);
    }

    private static BackgroundData BackgroundWithOneRole() => new()
    {
        Personal = new PersonalInfo { Name = "Jordan Rivers" },
        Experience =
        [
            new ExperienceEntry
            {
                Company = "Acme Corp", Role = "Engineer", Location = "Remote",
                Dates = new DateRange { Start = "2022-01", End = "2024-06" },
                CompanyDescription = "Acme makes widgets.",
                Achievements = ["Shipped feature A.", "Shipped feature B.", "Shipped feature C."],
            },
        ],
    };

    [Fact]
    public void Render_ExperienceWithNoOverrides_RendersAllAchievementsInOrderWithFormattedDates()
    {
        var output = ResumeRenderer.Render(BackgroundWithOneRole(), EmptyResume());

        Assert.Contains("### Engineer – Acme Corp\nRemote | Jan 2022 – Jun 2024", output);
        Assert.Contains("Acme makes widgets.", output);
        Assert.Contains("- Shipped feature A.\n- Shipped feature B.\n- Shipped feature C.", output);
    }

    [Fact]
    public void Render_OpenEndedRole_RendersPresent()
    {
        var background = BackgroundWithOneRole();
        background.Experience[0].Dates.End = null;

        var output = ResumeRenderer.Render(background, EmptyResume());

        Assert.Contains("Jan 2022 – Present", output);
    }

    [Fact]
    public void Render_AchievementTextOverride_ReplacesBackgroundWording()
    {
        var resume = EmptyResume();
        resume.ExperienceOverridesJson = JsonSerializer.Serialize(new List<ExperienceOverride>
        {
            new(0, true, null, [new ItemOverride(1, true, "Shipped feature B, rewritten for this application.")], [], null),
        });

        var output = ResumeRenderer.Render(BackgroundWithOneRole(), resume);

        Assert.Contains("- Shipped feature A.\n- Shipped feature B, rewritten for this application.\n- Shipped feature C.", output);
    }

    [Fact]
    public void Render_AchievementExcluded_OmitsThatBulletOnly()
    {
        var resume = EmptyResume();
        resume.ExperienceOverridesJson = JsonSerializer.Serialize(new List<ExperienceOverride>
        {
            new(0, true, null, [new ItemOverride(1, false, null)], [], null),
        });

        var output = ResumeRenderer.Render(BackgroundWithOneRole(), resume);

        Assert.Contains("- Shipped feature A.\n- Shipped feature C.", output);
        Assert.DoesNotContain("Shipped feature B.", output);
    }

    [Fact]
    public void Render_AchievementOrder_MovesItemAheadOfUntouchedOnes()
    {
        // Feature C (index 2) moved to lead the list via Order=0; A and B keep their natural
        // relative order (indices 0, 1) among the untouched items.
        var resume = EmptyResume();
        resume.ExperienceOverridesJson = JsonSerializer.Serialize(new List<ExperienceOverride>
        {
            new(0, true, null, [new ItemOverride(2, true, null, Order: 0)], [], null),
        });

        var output = ResumeRenderer.Render(BackgroundWithOneRole(), resume);

        Assert.Contains("- Shipped feature C.\n- Shipped feature A.\n- Shipped feature B.", output);
    }

    [Fact]
    public void Render_NoOrderSet_KeepsNaturalIndexOrder()
    {
        var output = ResumeRenderer.Render(BackgroundWithOneRole(), EmptyResume());

        Assert.Contains("- Shipped feature A.\n- Shipped feature B.\n- Shipped feature C.", output);
    }

    [Fact]
    public void Render_ExtraAchievements_AlwaysAppendAfterOrderedSurvivors()
    {
        var resume = EmptyResume();
        resume.ExperienceOverridesJson = JsonSerializer.Serialize(new List<ExperienceOverride>
        {
            new(0, true, null, [new ItemOverride(2, true, null, Order: 0)], ["Synthesized extra."], null),
        });

        var output = ResumeRenderer.Render(BackgroundWithOneRole(), resume);

        Assert.Contains("- Shipped feature C.\n- Shipped feature A.\n- Shipped feature B.\n- Synthesized extra.", output);
    }

    [Fact]
    public void Render_ExtraAchievement_AppendsSynthesizedBulletWithNoBackgroundSource()
    {
        // Confirmed necessary against real data: cv_base.md's real Willow entry has bullets with
        // no corresponding Background.Experience achievement at all.
        var resume = EmptyResume();
        resume.ExperienceOverridesJson = JsonSerializer.Serialize(new List<ExperienceOverride>
        {
            new(0, true, null, [], ["Synthesized bullet with no Background source."], null),
        });

        var output = ResumeRenderer.Render(BackgroundWithOneRole(), resume);

        Assert.Contains("- Shipped feature A.\n- Shipped feature B.\n- Shipped feature C.\n- Synthesized bullet with no Background source.", output);
    }

    [Fact]
    public void Render_CompanyDescriptionOverride_ReplacesBackgroundWording()
    {
        var resume = EmptyResume();
        resume.ExperienceOverridesJson = JsonSerializer.Serialize(new List<ExperienceOverride>
        {
            new(0, true, "Shorter tailored description.", [], [], null),
        });

        var output = ResumeRenderer.Render(BackgroundWithOneRole(), resume);

        Assert.Contains("Shorter tailored description.", output);
        Assert.DoesNotContain("Acme makes widgets.", output);
    }

    [Fact]
    public void Render_ExperienceEntryExcluded_OmitsEntireEntry_EpicLankaStyle()
    {
        var background = BackgroundWithOneRole();
        background.Experience.Add(new ExperienceEntry { Company = "Weakest Co", Role = "Intern", Achievements = ["Did an intern thing."] });
        var resume = EmptyResume();
        resume.ExperienceOverridesJson = JsonSerializer.Serialize(new List<ExperienceOverride>
        {
            new(1, false, null, [], [], "Weakest role — only include for design/early-career-focused applications."),
        });

        var output = ResumeRenderer.Render(background, resume);

        Assert.Contains("Acme Corp", output);
        Assert.DoesNotContain("Weakest Co", output);
        Assert.DoesNotContain("Did an intern thing.", output);
    }

    [Fact]
    public void Render_Education_RendersDirectlyFromBackgroundNoOverrideNeeded()
    {
        var background = MinimalBackground();
        background.Education.Add(new EducationEntry { Degree = "BSc Computer Science", Institution = "State University", Location = "Remote", GraduationYear = 2020 });

        var output = ResumeRenderer.Render(background, EmptyResume());

        Assert.Contains("## Education\n\n**BSc Computer Science** – State University\nRemote | 2020", output);
    }

    [Fact]
    public void Render_EducationWithNoGraduationYear_OmitsItInsteadOfRenderingZero()
    {
        // Real production bug, found generating an actual CV against a real account after
        // Deploy B shipped: GraduationYear was a non-nullable int, and a real user's Background
        // (data drift from the repo's seed fixture, not a parsing error) genuinely has no
        // graduation_year at all — it silently defaulted to 0 and rendered a literal "| 0" in a
        // real generated resume.
        var background = MinimalBackground();
        background.Education.Add(new EducationEntry { Degree = "BSc Computer Science", Institution = "State University", Location = "Remote" });

        var output = ResumeRenderer.Render(background, EmptyResume());

        Assert.Contains("## Education\n\n**BSc Computer Science** – State University\nRemote\n", output);
        Assert.DoesNotContain("| 0", output);
    }

    [Fact]
    public void Render_EducationWithNeitherLocationNorGraduationYear_OmitsMetaLineEntirely()
    {
        var background = MinimalBackground();
        background.Education.Add(new EducationEntry { Degree = "BSc Computer Science", Institution = "State University" });

        var output = ResumeRenderer.Render(background, EmptyResume());

        Assert.Contains("**BSc Computer Science** – State University", output);
        // No meta line at all — degree/institution line is followed straight by the next
        // section (or end of document), not a dangling blank "| "-style line.
        Assert.DoesNotContain(" | \n", output);
    }

    [Fact]
    public void Render_SkillsSection_RendersStoredLabelsAndItemsNotBackgroundSkills()
    {
        var resume = EmptyResume();
        resume.SkillsSectionJson = JsonSerializer.Serialize(new List<SkillsSectionEntry>
        {
            new("Languages", ["C#", "TypeScript"]),
            new("Cloud", ["Azure (App Services, Functions)", "AWS"]),
        });

        var output = ResumeRenderer.Render(MinimalBackground(), resume);

        Assert.Contains("## Skills\n\n**Languages** – C#, TypeScript\n**Cloud** – Azure (App Services, Functions), AWS", output);
    }

    [Fact]
    public void Render_EmptySkillsSection_OmitsSkillsHeadingEntirely()
    {
        var output = ResumeRenderer.Render(MinimalBackground(), EmptyResume());

        Assert.DoesNotContain("## Skills", output);
    }

    [Fact]
    public void Render_SkillsGroupWithEmptyLabelButItems_RendersItemsWithoutBareBoldMarkers()
    {
        // Real production bug: a skills group added via the Resume Builder's Skills editor with
        // Items filled in but no Label typed yet rendered a literal "****" (bold markers with
        // nothing between them) in real generated resumes.
        var resume = EmptyResume();
        resume.SkillsSectionJson = JsonSerializer.Serialize(new List<SkillsSectionEntry>
        {
            new("", ["C#", "TypeScript"]),
        });

        var output = ResumeRenderer.Render(MinimalBackground(), resume);

        Assert.Contains("## Skills\n\nC#, TypeScript", output);
        Assert.DoesNotContain("****", output);
    }

    [Fact]
    public void Render_SkillsGroupWithEmptyLabelAndNoItems_SkipsThatGroupEntirely()
    {
        var resume = EmptyResume();
        resume.SkillsSectionJson = JsonSerializer.Serialize(new List<SkillsSectionEntry>
        {
            new("", []),
            new("Languages", ["C#"]),
        });

        var output = ResumeRenderer.Render(MinimalBackground(), resume);

        Assert.Contains("## Skills\n\n**Languages** – C#", output);
        Assert.DoesNotContain("****", output);
    }

    [Fact]
    public void Render_AllSkillsGroupsHaveEmptyLabelAndNoItems_OmitsSkillsHeadingEntirely()
    {
        var resume = EmptyResume();
        resume.SkillsSectionJson = JsonSerializer.Serialize(new List<SkillsSectionEntry>
        {
            new("", []),
        });

        var output = ResumeRenderer.Render(MinimalBackground(), resume);

        Assert.DoesNotContain("## Skills", output);
    }

    [Fact]
    public void Render_Projects_AppliesDescriptionAndHighlightOverridesAndExtras()
    {
        var background = MinimalBackground();
        background.Projects.Add(new ProjectEntry { Name = "Side Project", Description = "Long raw description.", Highlights = ["Highlight one.", "Highlight two."] });
        var resume = EmptyResume();
        resume.ProjectOverridesJson = JsonSerializer.Serialize(new List<ProjectOverride>
        {
            new(0, true, "Short tailored description.", [new ItemOverride(1, false, null)], ["Extra highlight with no source."]),
        });

        var output = ResumeRenderer.Render(background, resume);

        Assert.Contains("### Side Project", output);
        Assert.Contains("Short tailored description.", output);
        Assert.Contains("- Highlight one.\n- Extra highlight with no source.", output);
        Assert.DoesNotContain("Highlight two.", output);
        Assert.DoesNotContain("Long raw description.", output);
    }

    [Fact]
    public void Render_Credentials_RendersKindNameIssuerAndStatusMetadata()
    {
        var background = MinimalBackground();
        background.Credentials.Add(new CredentialEntry { Kind = "license", Name = "Registered Nurse", Issuer = "State Nursing Board", Status = "Active", ExpiryDate = "2027-06" });
        var resume = EmptyResume();
        resume.SectionConfigJson = JsonSerializer.Serialize(new List<SectionConfigEntry> { new("credentials", true) });

        var output = ResumeRenderer.Render(background, resume);

        Assert.Contains("## Credentials\n\n**Registered Nurse** – State Nursing Board\nActive | Expires 2027-06", output);
    }

    [Fact]
    public void Render_Volunteering_RendersRoleOrgAndDateRange()
    {
        var background = MinimalBackground();
        background.Volunteering.Add(new VolunteeringEntry { Role = "Mentor", Org = "Community Org", Dates = new DateRange { Start = "2023-01", End = "2023-12" }, Description = "Mentored newcomers." });
        var resume = EmptyResume();
        resume.SectionConfigJson = JsonSerializer.Serialize(new List<SectionConfigEntry> { new("volunteering", true) });

        var output = ResumeRenderer.Render(background, resume);

        Assert.Contains("## Volunteering & Leadership\n\n**Mentor, Community Org** | Jan 2023 – Dec 2023\nMentored newcomers.", output);
    }

    [Fact]
    public void Render_SectionConfigExcludesSection_OmitsItEvenWithData()
    {
        var background = BackgroundWithOneRole();
        var resume = EmptyResume();
        resume.SectionConfigJson = JsonSerializer.Serialize(new List<SectionConfigEntry> { new("experience", false) });

        var output = ResumeRenderer.Render(background, resume);

        Assert.DoesNotContain("## Experience", output);
        Assert.DoesNotContain("Acme Corp", output);
    }

    [Fact]
    public void Render_SectionConfigOrder_ControlsOutputOrder()
    {
        var background = BackgroundWithOneRole();
        background.Education.Add(new EducationEntry { Degree = "BSc", Institution = "Uni", GraduationYear = 2019 });
        var resume = EmptyResume();
        resume.SectionConfigJson = JsonSerializer.Serialize(new List<SectionConfigEntry>
        {
            new("education", true),
            new("experience", true),
        });

        var output = ResumeRenderer.Render(background, resume);

        Assert.True(output.IndexOf("## Education", StringComparison.Ordinal) < output.IndexOf("## Experience", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_MissingSectionConfig_UsesDefaultOrderMatchingTodaysCvBase()
    {
        var background = BackgroundWithOneRole();
        background.Education.Add(new EducationEntry { Degree = "BSc", Institution = "Uni", GraduationYear = 2019 });
        var resume = EmptyResume(); // SectionConfigJson = "[]"

        var output = ResumeRenderer.Render(background, resume);

        Assert.True(output.IndexOf("## Experience", StringComparison.Ordinal) < output.IndexOf("## Education", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_RealFixture_DoesNotThrowAndIncludesRealContent()
    {
        var background = BackgroundYamlParser.Parse(SkillLoader.Load("context/background.yaml"));
        var output = ResumeRenderer.Render(background, EmptyResume());

        Assert.Contains("# Kavin Abeysinghe", output);
        Assert.Contains("## Experience", output);
        Assert.Contains("### Software Engineer – Willow Inc.", output);
        Assert.Contains("## Education", output);
        Assert.Contains("## Volunteering & Leadership", output);
        // Skills isn't populated yet (SkillsSectionJson is empty until backfill runs) — must not
        // render an empty heading.
        Assert.DoesNotContain("## Skills", output);
    }
}
