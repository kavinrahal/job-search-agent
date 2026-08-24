using System.Text.Json;

namespace JobSearch.Data;

// Shared tool-use JSON schema builders and response parsers for the UserResume override shapes
// (SectionConfigEntry, ItemOverride, ExperienceOverride, SkillsSectionEntry, ProjectOverride).
// Extracted from ResumeBackfillAgent (the original, one-time-migration user of these shapes) so
// CvTailorAgent's per-application tailoring calls — which need the identical shapes, just
// populated by different judgment rules — don't duplicate the schema/parsing logic.
internal static class ResumeOverrideSchema
{
    public static List<SectionConfigEntry> ExtractSectionConfig(IReadOnlyDictionary<string, JsonElement> input, string key)
    {
        if (!input.TryGetValue(key, out var el) || el.ValueKind != JsonValueKind.Array) return [];
        return [.. el.EnumerateArray().Select(e => new SectionConfigEntry(
            GetString(e, "section_key") ?? "",
            GetBool(e, "included")))];
    }

    public static List<ExperienceOverride> ExtractExperienceOverrides(IReadOnlyDictionary<string, JsonElement> input, string key)
    {
        if (!input.TryGetValue(key, out var el) || el.ValueKind != JsonValueKind.Array) return [];
        return [.. el.EnumerateArray().Select(e => new ExperienceOverride(
            ExperienceIndex: GetInt(e, "experience_index"),
            Included: GetBool(e, "included", defaultValue: true),
            CompanyDescriptionOverride: GetString(e, "company_description_override"),
            Achievements: ExtractItemOverrides(e, "achievements"),
            ExtraAchievements: GetStringArray(e, "extra_achievements"),
            Notes: GetString(e, "notes")))];
    }

    // Shared by experience achievements and project highlights — same override shape.
    public static List<ItemOverride> ExtractItemOverrides(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Array) return [];
        return [.. el.EnumerateArray().Select(e => new ItemOverride(
            GetInt(e, "index"),
            GetBool(e, "included", defaultValue: true),
            GetString(e, "text_override"),
            GetNullableInt(e, "order")))];
    }

    public static List<SkillsSectionEntry> ExtractSkillsSection(IReadOnlyDictionary<string, JsonElement> input, string key)
    {
        if (!input.TryGetValue(key, out var el) || el.ValueKind != JsonValueKind.Array) return [];
        return [.. el.EnumerateArray().Select(e => new SkillsSectionEntry(
            GetString(e, "label") ?? "",
            GetStringArray(e, "items")))];
    }

    public static List<ProjectOverride> ExtractProjectOverrides(IReadOnlyDictionary<string, JsonElement> input, string key)
    {
        if (!input.TryGetValue(key, out var el) || el.ValueKind != JsonValueKind.Array) return [];
        return [.. el.EnumerateArray().Select(e => new ProjectOverride(
            ProjectIndex: GetInt(e, "project_index"),
            Included: GetBool(e, "included", defaultValue: true),
            DescriptionOverride: GetString(e, "description_override"),
            Highlights: ExtractItemOverrides(e, "highlights"),
            ExtraHighlights: GetStringArray(e, "extra_highlights")))];
    }

    private static string? GetString(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool GetBool(JsonElement e, string prop, bool defaultValue = false) =>
        e.TryGetProperty(prop, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False) ? v.GetBoolean() : defaultValue;

    private static int GetInt(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : -1;

    private static int? GetNullableInt(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;

    private static List<string> GetStringArray(JsonElement e, string prop)
    {
        if (!e.TryGetProperty(prop, out var v) || v.ValueKind != JsonValueKind.Array) return [];
        return [.. v.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0)];
    }

    public static JsonElement Prop(string type, string description) =>
        JsonSerializer.SerializeToElement(new { type, description });

    public static JsonElement PropSectionConfigArray() => JsonSerializer.SerializeToElement(new
    {
        type = "array",
        description = "One entry per section key, in the resume's actual order — every key listed even if included is false.",
        items = new
        {
            type = "object",
            properties = new
            {
                section_key = new { type = "string", @enum = new[] { "experience", "education", "skills", "credentials", "publications", "volunteering", "projects" } },
                included = new { type = "boolean" },
            },
            required = new[] { "section_key", "included" },
        },
    });

    // Shared by experience achievements and project highlights — same override shape, on the
    // schema side too. order is optional: omit it to keep an item's natural (Background) position.
    public static JsonElement PropItemOverrideArray(string description) => JsonSerializer.SerializeToElement(new
    {
        type = "array",
        description,
        items = new
        {
            type = "object",
            properties = new
            {
                index = new { type = "integer" },
                included = new { type = "boolean" },
                text_override = new { type = "string" },
                order = new { type = "integer", description = "Set only to move this item to a specific position in the rendered list; omit to keep its natural position." },
            },
            required = new[] { "index", "included" },
        },
    });

    public static JsonElement PropExperienceOverrideArray(string achievementsDescription) => JsonSerializer.SerializeToElement(new
    {
        type = "array",
        description = "One entry per BACKGROUND experience index, in order.",
        items = new
        {
            type = "object",
            properties = new
            {
                experience_index = new { type = "integer" },
                included = new { type = "boolean" },
                company_description_override = new { type = "string", description = "Only if the rendered wording should differ from BACKGROUND's; omit/null otherwise." },
                achievements = PropItemOverrideArray(achievementsDescription),
                extra_achievements = new { type = "array", items = new { type = "string" }, description = "Bullets for this role with no BACKGROUND source." },
                notes = new { type = "string" },
            },
            required = new[] { "experience_index", "included", "achievements", "extra_achievements" },
        },
    });

    public static JsonElement PropSkillsSectionArray() => JsonSerializer.SerializeToElement(new
    {
        type = "array",
        items = new
        {
            type = "object",
            properties = new
            {
                label = new { type = "string" },
                items = new { type = "array", items = new { type = "string" } },
            },
            required = new[] { "label", "items" },
        },
    });

    public static JsonElement PropProjectOverrideArray(string highlightsDescription) => JsonSerializer.SerializeToElement(new
    {
        type = "array",
        description = "One entry per BACKGROUND project index, in order.",
        items = new
        {
            type = "object",
            properties = new
            {
                project_index = new { type = "integer" },
                included = new { type = "boolean" },
                description_override = new { type = "string" },
                highlights = PropItemOverrideArray(highlightsDescription),
                extra_highlights = new { type = "array", items = new { type = "string" } },
            },
            required = new[] { "project_index", "included", "highlights", "extra_highlights" },
        },
    });
}
