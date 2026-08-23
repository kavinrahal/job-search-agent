namespace JobSearch.Data;

// C# shapes for UserResume's JSON string columns (System.Text.Json, same convention as
// AgentThread.HistoryJson etc.) — kept as separate small records from BackgroundData since
// these describe curation/presentation, not candidate facts.

public record SectionConfigEntry(string SectionKey, bool Included);

// Shared by ExperienceOverride.Achievements and ProjectOverride.Highlights — same shape, no
// reason for two identically-fielded record types.
public record ItemOverride(int Index, bool Included, string? TextOverride);

// ExtraAchievements: confirmed necessary against real data, not speculative — cv_base.md's real
// Willow entry has two bullets (an ASP.NET Core/React one, a documentation one) with no
// corresponding Background.Experience[i].Achievements entry at all, synthesized rather than
// edited from one. Appended after the indexed achievements; Phase 1 has no builder UI for a user
// to control finer bullet ordering, so this is an acceptable simplification until Phase 2.
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
