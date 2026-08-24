namespace JobSearch.Data;

// 1:1 with User, same pattern and reasoning as UserProfile (see its own header comment) — always
// looked up by an exact known UserId, never a broad list query, so no query filter.
//
// This is the presentation layer over UserProfile.Background's raw facts, not a second copy of
// them: Experience/Skills/Projects content lives in Background, this table stores only what
// Background alone can't reconstruct — curation (what to include, in what order) and the real,
// confirmed-necessary rewrites (see JobSearch.Data/skills/context comparison that motivated this
// schema: cv_base.md's actual content diverges from background.yaml's raw wording/grouping in
// several places, not just order). Row absence means "not yet migrated from the old CvBase text
// blob" — see the resume-builder Phase 1 migration.
public class UserResume
{
    public int UserId { get; set; }

    // The base/default summary. Previously always an AI-regenerated placeholder baked into
    // CvBase text; now an actual stored, user-editable value.
    public string Summary { get; set; } = "";

    // Ordered list of {sectionKey, included} — JSON, System.Text.Json, same convention as
    // AgentThread.HistoryJson/EvaluationJson (plain string column, not a native jsonb mapping).
    // sectionKey values: experience, education, skills, credentials, publications, volunteering,
    // projects. Seeded at migration time to match today's fixed cv_base.md section order.
    public string SectionConfigJson { get; set; } = "[]";

    // Per-experience-entry overrides, referencing Background.Experience by positional index
    // (not a stored id — see the schema-extension notes on why: BackgroundEditor.tsx ships live
    // today and types achievements as string[], so adding stored ids there would break it).
    // Entries absent here render unedited, in Background's own order. Only present where
    // cv_base.md's real content diverges from Background's raw achievement text.
    public string ExperienceOverridesJson { get; set; } = "[]";

    // The actual rendered Skills section: list of {label, items[]}. NOT derived from
    // Background.Skills at render time — confirmed by direct comparison that the same nested
    // shape gets merged into one line for some categories (Languages) and split into several for
    // others (Frameworks' backend/frontend/testing), with no deterministic rule connecting the
    // two. Background.Skills stays reference inventory for tailoring; this is what's shown.
    public string SkillsSectionJson { get; set; } = "[]";

    // Same shape/reasoning as ExperienceOverridesJson, applied to Background.Projects.
    public string ProjectOverridesJson { get; set; } = "[]";

    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
