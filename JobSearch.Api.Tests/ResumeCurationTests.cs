using System.Text.Json;
using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Api.Tests;

// Tests JobSearch.Data.ResumeCuration — the logic behind both PUT /resume (ApplyUpdate,
// persisted) and POST /resume/preview (Preview, transient/unsaved). DB-backed cases use the
// same InMemory-provider pattern as UserProfileProvisioningServiceTests, since the point of
// these tests is exactly the persistence boundary: what does/doesn't reach the database.
public class ResumeCurationTests
{
    private static AppDbContext Db(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options);

    private static AppDbContext FreshDb() => Db(Guid.NewGuid().ToString());

    private static UserResume FullResume(int userId = 1) => new()
    {
        UserId = userId,
        Summary = "Original summary.",
        SectionConfigJson = JsonSerializer.Serialize(new List<SectionConfigEntry> { new("experience", true) }),
        ExperienceOverridesJson = JsonSerializer.Serialize(new List<ExperienceOverride> { new(0, true, "Original description.", [], [], null) }),
        ProjectOverridesJson = JsonSerializer.Serialize(new List<ProjectOverride> { new(0, true, "Original project description.", [], []) }),
        SkillsSectionJson = JsonSerializer.Serialize(new List<SkillsSectionEntry> { new("Languages", ["C#"]) }),
        UpdatedAt = DateTime.UtcNow,
    };

    private static BackgroundData BackgroundWithOneRoleAndProject() => new()
    {
        Personal = new PersonalInfo { Name = "Jordan Rivers" },
        Experience = [new ExperienceEntry { Company = "Acme", Role = "Engineer", CompanyDescription = "Acme makes widgets.", Achievements = ["Did A."] }],
        Projects = [new ProjectEntry { Name = "Side Project", Description = "Raw description.", Highlights = ["H1"] }],
    };

    // TC01 — ApplyUpdate only changes fields that were actually provided; everything else on
    // the resume (including fields this update never mentions) is left exactly as it was.
    [Fact]
    public void ApplyUpdate_OnlyProvidedFieldsChange_RestStayUntouched()
    {
        var resume = FullResume();
        var originalSectionConfig = resume.SectionConfigJson;
        var originalProjectOverrides = resume.ProjectOverridesJson;
        var originalSkillsSection = resume.SkillsSectionJson;

        ResumeCuration.ApplyUpdate(
            resume,
            summary: "New summary.",
            sectionConfig: null,
            experienceOverrides: [new ExperienceOverride(0, false, "New description.", [], [], null)],
            projectOverrides: null,
            skillsSection: null);

        Assert.Equal("New summary.", resume.Summary);
        Assert.Contains("New description.", resume.ExperienceOverridesJson);
        Assert.Equal(originalSectionConfig, resume.SectionConfigJson);
        Assert.Equal(originalProjectOverrides, resume.ProjectOverridesJson);
        Assert.Equal(originalSkillsSection, resume.SkillsSectionJson);
    }

    // TC02 — Calling ApplyUpdate with every field null is a true no-op (the "PUT /resume with
    // an empty body" edge case) — nothing changes, not even to an equivalent-but-different
    // serialization.
    [Fact]
    public void ApplyUpdate_AllFieldsNull_ChangesNothing()
    {
        var resume = FullResume();
        var before = (resume.Summary, resume.SectionConfigJson, resume.ExperienceOverridesJson, resume.ProjectOverridesJson, resume.SkillsSectionJson);

        ResumeCuration.ApplyUpdate(resume, null, null, null, null, null);

        Assert.Equal(before, (resume.Summary, resume.SectionConfigJson, resume.ExperienceOverridesJson, resume.ProjectOverridesJson, resume.SkillsSectionJson));
    }

    // TC03 — End-to-end through a real (InMemory) DbContext, mirroring exactly what PUT /resume
    // does: fetch, ApplyUpdate with only some fields set, SaveChangesAsync. Reloading from the
    // db confirms the partial-update semantics survive a real persistence round trip, not just
    // in-memory mutation.
    [Fact]
    public async Task ApplyUpdate_ThenSave_PersistsOnlyProvidedFields()
    {
        var dbName = Guid.NewGuid().ToString();
        using (var db = Db(dbName))
        {
            db.UserResumes.Add(FullResume());
            await db.SaveChangesAsync();

            var resume = await db.UserResumes.FindAsync(1);
            ResumeCuration.ApplyUpdate(
                resume!,
                summary: null,
                sectionConfig: null,
                experienceOverrides: null,
                projectOverrides: null,
                skillsSection: [new SkillsSectionEntry("Cloud", ["Azure"])]);
            await db.SaveChangesAsync();
        }

        // Fresh DbContext instance against the same InMemory store — a genuine reload, not the
        // same tracked object the update above already mutated.
        using var reloadDb = Db(dbName);
        var reloaded = await reloadDb.UserResumes.FindAsync(1);
        Assert.Contains("Cloud", reloaded!.SkillsSectionJson);
        Assert.Contains("Azure", reloaded.SkillsSectionJson);
        Assert.Equal("Original summary.", reloaded.Summary);
        Assert.Contains("Original description.", reloaded.ExperienceOverridesJson);
        Assert.Contains("Original project description.", reloaded.ProjectOverridesJson);
    }

    // TC04 — Preview renders draft fields where the caller provides them...
    [Fact]
    public void Preview_UsesDraftFieldsWhereProvided()
    {
        var output = ResumeCuration.Preview(
            BackgroundWithOneRoleAndProject(),
            FullResume(),
            summary: "Draft summary for preview.",
            sectionConfig: [new SectionConfigEntry("experience", true)],
            experienceOverrides: null,
            projectOverrides: null,
            skillsSection: null);

        Assert.Contains("Draft summary for preview.", output);
        Assert.DoesNotContain("Original summary.", output);
    }

    // TC05 — ...and falls back to the base resume's real stored values for anything the draft
    // omits (null), same partial semantics as ApplyUpdate/PUT — a preview of "just the summary
    // changed" must still show the real, currently-saved experience overrides.
    [Fact]
    public void Preview_FallsBackToBaseResumeFieldsWhenDraftOmitsThem()
    {
        var output = ResumeCuration.Preview(
            BackgroundWithOneRoleAndProject(),
            FullResume(),
            summary: "Draft summary.",
            sectionConfig: [new SectionConfigEntry("experience", true), new SectionConfigEntry("projects", true)],
            experienceOverrides: null, // omitted -> base resume's stored override applies
            projectOverrides: null,    // omitted -> base resume's stored override applies
            skillsSection: null);

        Assert.Contains("Original description.", output);
        Assert.Contains("Original project description.", output);
    }

    // TC06 — The core guarantee POST /resume/preview depends on: Preview never mutates the
    // resume/db it was given, and never writes anything — confirmed both by checking the
    // ChangeTracker sees no pending changes and by reloading the row from the db afterward.
    [Fact]
    public async Task Preview_DoesNotModifyOrSaveTheStoredResume()
    {
        using var db = FreshDb();
        db.UserResumes.Add(FullResume());
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var resume = await db.UserResumes.FindAsync(1);
        var markdown = ResumeCuration.Preview(
            BackgroundWithOneRoleAndProject(),
            resume!,
            summary: "A completely different draft summary.",
            sectionConfig: [new SectionConfigEntry("experience", true)],
            experienceOverrides: [new ExperienceOverride(0, false, "Draft-only description, never saved.", [], [], null)],
            projectOverrides: null,
            skillsSection: null);

        Assert.Contains("A completely different draft summary.", markdown);
        Assert.False(db.ChangeTracker.HasChanges(), "Preview must not leave pending changes on the DbContext it was given.");

        var reloaded = await db.UserResumes.FindAsync(1);
        Assert.Equal("Original summary.", reloaded!.Summary);
        Assert.DoesNotContain("Draft-only description, never saved.", reloaded.ExperienceOverridesJson);
    }
}
