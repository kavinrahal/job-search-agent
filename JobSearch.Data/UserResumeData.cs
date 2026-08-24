namespace JobSearch.Data;

// C# shapes for UserResume's JSON string columns (System.Text.Json, same convention as
// AgentThread.HistoryJson etc.) — kept as separate small records from BackgroundData since
// these describe curation/presentation, not candidate facts.

public record SectionConfigEntry(string SectionKey, bool Included);

// Shared by ExperienceOverride.Achievements and ProjectOverride.Highlights — same shape, no
// reason for two identically-fielded record types. Order: null keeps the item's natural position
// (its index in Background's own list); set it to move an item to lead the rendered list — this
// is what makes tailor_cv.md's "reorder bullets within a role" rule expressible in the structured
// model (CvTailorAgent's per-application delta), not just include/exclude/reword-in-place.
public record ItemOverride(int Index, bool Included, string? TextOverride, int? Order = null);

// ExtraAchievements: confirmed necessary against real data, not speculative — cv_base.md's real
// Willow entry has two bullets (an ASP.NET Core/React one, a documentation one) with no
// corresponding Background.Experience[i].Achievements entry at all, synthesized rather than
// edited from one. Always appended after the (now orderable) indexed achievements — extras have
// no natural position to be ordered relative to, and no rule anywhere asks for one.
public record ExperienceOverride(
    int ExperienceIndex,
    bool Included,
    string? CompanyDescriptionOverride,
    List<ItemOverride> Achievements,
    List<string> ExtraAchievements,
    string? Notes);

public record SkillsSectionEntry(string Label, List<string> Items);

public record ProjectOverride(
    int ProjectIndex,
    bool Included,
    string? DescriptionOverride,
    List<ItemOverride> Highlights,
    List<string> ExtraHighlights);
