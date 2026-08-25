using System.Text.Json;

namespace JobSearch.Data;

// Shared "apply a partial curation update over a base UserResume" logic behind both
// PUT /resume (persists the result on the real row) and POST /resume/preview (renders a
// transient, unsaved copy against the real ResumeRenderer). Same five optional fields, same
// partial-update semantics (null means "leave this field unchanged") in both places, so this
// lives in one spot instead of being duplicated across the two endpoints.
public static class ResumeCuration
{
    // Mutates `resume`'s fields in place for whichever parameters are non-null — the PUT
    // /resume partial-update semantics. Caller is responsible for UpdatedAt and
    // SaveChangesAsync (this never touches the database itself).
    public static void ApplyUpdate(
        UserResume resume,
        string? summary,
        List<SectionConfigEntry>? sectionConfig,
        List<ExperienceOverride>? experienceOverrides,
        List<ProjectOverride>? projectOverrides,
        List<SkillsSectionEntry>? skillsSection)
    {
        if (summary is not null) resume.Summary = summary;
        if (sectionConfig is not null) resume.SectionConfigJson = JsonSerializer.Serialize(sectionConfig);
        if (experienceOverrides is not null) resume.ExperienceOverridesJson = JsonSerializer.Serialize(experienceOverrides);
        if (projectOverrides is not null) resume.ProjectOverridesJson = JsonSerializer.Serialize(projectOverrides);
        if (skillsSection is not null) resume.SkillsSectionJson = JsonSerializer.Serialize(skillsSection);
    }

    // Builds a transient (never added to any DbContext, never saved) UserResume: draft fields
    // where provided, falling back to `baseResume`'s real stored values otherwise — then
    // renders it with the real ResumeRenderer. Mirrors CvTailorAgent.ApplyDeltaAndRender's
    // "transient render, no persistence" shape, for the same reason: the preview must be the
    // actual renderer's output, not a second reimplementation of its merge logic.
    public static string Preview(
        BackgroundData background,
        UserResume baseResume,
        string? summary,
        List<SectionConfigEntry>? sectionConfig,
        List<ExperienceOverride>? experienceOverrides,
        List<ProjectOverride>? projectOverrides,
        List<SkillsSectionEntry>? skillsSection)
    {
        // Start from a copy of the real stored values, then apply the exact same partial-update
        // rule ApplyUpdate uses for PUT /resume — one implementation of "field provided -> use
        // it, else keep base" instead of a second, parallel ternary version of it here.
        var transient = new UserResume
        {
            UserId = baseResume.UserId,
            Summary = baseResume.Summary,
            SectionConfigJson = baseResume.SectionConfigJson,
            ExperienceOverridesJson = baseResume.ExperienceOverridesJson,
            ProjectOverridesJson = baseResume.ProjectOverridesJson,
            SkillsSectionJson = baseResume.SkillsSectionJson,
        };
        ApplyUpdate(transient, summary, sectionConfig, experienceOverrides, projectOverrides, skillsSection);
        return ResumeRenderer.Render(background, transient);
    }
}
