using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace JobSearch.Data;

public record ResumeBackfillResult(
    string Summary,
    List<SectionConfigEntry> SectionConfig,
    List<ExperienceOverride> ExperienceOverrides,
    List<SkillsSectionEntry> SkillsSection,
    List<ProjectOverride> ProjectOverrides);

// One-time migration from the old free-text CvBase to the new structured UserResume, for one
// user at a time (see Program.cs's admin backfill endpoint). Four separate parallel tool-use
// calls, same reasoning as ResumeIntakeAgent's split: each field gets its own full token budget
// rather than competing for one, avoiding the truncation failure that pattern was built to fix.
public class ResumeBackfillAgent
{
    private readonly AnthropicClient _client;
    private const string SonnetModel = "claude-sonnet-5";
    private const int MaxTokens = 4000;
    private readonly string _skillText;
    private readonly ClaudeUsageLogger? _usageLogger;

    private readonly Tool _summaryConfigTool;
    private readonly Tool _experienceTool;
    private readonly Tool _skillsTool;
    private readonly Tool _projectsTool;

    public ResumeBackfillAgent(string apiKey, ClaudeUsageLogger? usageLogger = null)
    {
        _client = new AnthropicClient { ApiKey = apiKey };
        _skillText = SkillLoader.Load("backfill_resume.md");
        _usageLogger = usageLogger;

        _summaryConfigTool = new Tool
        {
            Name = "submit_summary_and_config",
            Description = "Submit the extracted Summary text and section order/inclusion.",
            InputSchema = new InputSchema
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["summary"] = Prop("string", "CV_BASE's Summary section text, or empty string if it's only the placeholder."),
                    ["section_config"] = PropSectionConfigArray(),
                },
                Required = ["summary", "section_config"],
            },
            CacheControl = new CacheControlEphemeral(),
        };

        _experienceTool = new Tool
        {
            Name = "submit_experience_overrides",
            Description = "Submit per-experience-entry overrides describing how CV_BASE diverges from BACKGROUND.",
            InputSchema = new InputSchema
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["experience_overrides"] = PropExperienceOverrideArray(),
                },
                Required = ["experience_overrides"],
            },
            CacheControl = new CacheControlEphemeral(),
        };

        _skillsTool = new Tool
        {
            Name = "submit_skills_section",
            Description = "Submit CV_BASE's Skills section transcribed exactly as label/items pairs.",
            InputSchema = new InputSchema
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["skills_section"] = PropSkillsSectionArray(),
                },
                Required = ["skills_section"],
            },
            CacheControl = new CacheControlEphemeral(),
        };

        _projectsTool = new Tool
        {
            Name = "submit_project_overrides",
            Description = "Submit per-project overrides describing how CV_BASE diverges from BACKGROUND.",
            InputSchema = new InputSchema
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["project_overrides"] = PropProjectOverrideArray(),
                },
                Required = ["project_overrides"],
            },
            CacheControl = new CacheControlEphemeral(),
        };
    }

    private static string BuildUserContent(string backgroundYaml, string cvBaseMarkdown) => $"""
        --- BACKGROUND ---
        {backgroundYaml}

        --- CV_BASE ---
        {cvBaseMarkdown}
        """;

    public async Task<ResumeBackfillResult> BackfillAsync(int userId, string backgroundYaml, string cvBaseMarkdown)
    {
        var userContent = BuildUserContent(backgroundYaml, cvBaseMarkdown);

        var summaryConfigTask = CallAsync(userId, userContent, _summaryConfigTool);
        var experienceTask = CallAsync(userId, userContent, _experienceTool);
        var skillsTask = CallAsync(userId, userContent, _skillsTool);
        var projectsTask = CallAsync(userId, userContent, _projectsTool);
        await Task.WhenAll(summaryConfigTask, experienceTask, skillsTask, projectsTask);

        var summaryConfigInput = summaryConfigTask.Result;
        return new ResumeBackfillResult(
            Summary: summaryConfigInput.TryGetValue("summary", out var s) ? s.GetString() ?? "" : "",
            SectionConfig: ExtractSectionConfig(summaryConfigInput, "section_config"),
            ExperienceOverrides: ExtractExperienceOverrides(experienceTask.Result, "experience_overrides"),
            SkillsSection: ExtractSkillsSection(skillsTask.Result, "skills_section"),
            ProjectOverrides: ExtractProjectOverrides(projectsTask.Result, "project_overrides"));
    }

    private async Task<IReadOnlyDictionary<string, JsonElement>> CallAsync(int userId, string userContent, Tool tool)
    {
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = SonnetModel,
            MaxTokens = MaxTokens,
            System = new List<TextBlockParam>
            {
                new() { Text = _skillText, CacheControl = new CacheControlEphemeral() },
            },
            Tools = [tool],
            ToolChoice = new ToolChoiceAny(),
            Messages = [new() { Role = Role.User, Content = userContent }],
        });

        if (_usageLogger is not null)
            await _usageLogger.LogAsync(userId, ClaudeAgentName.ResumeBackfillAgent, SonnetModel, response.Usage);

        foreach (var block in response.Content)
            if (block.TryPickToolUse(out ToolUseBlock? toolUse))
                return toolUse.Input;

        throw new InvalidOperationException($"Resume backfill did not return a tool use block for \"{tool.Name}\".");
    }

    // Extraction methods split out and made static/testable, same principle as
    // ResumeIntakeAgent.ExtractField — verifiable without a live API call.

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
    private static List<ItemOverride> ExtractItemOverrides(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Array) return [];
        return [.. el.EnumerateArray().Select(e => new ItemOverride(
            GetInt(e, "index"),
            GetBool(e, "included", defaultValue: true),
            GetString(e, "text_override")))];
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

    private static List<string> GetStringArray(JsonElement e, string prop)
    {
        if (!e.TryGetProperty(prop, out var v) || v.ValueKind != JsonValueKind.Array) return [];
        return [.. v.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0)];
    }

    private static JsonElement Prop(string type, string description) =>
        JsonSerializer.SerializeToElement(new { type, description });

    private static JsonElement PropSectionConfigArray() => JsonSerializer.SerializeToElement(new
    {
        type = "array",
        description = "One entry per section key, in CV_BASE's actual order — every key listed even if included is false.",
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
    // schema side too.
    private static JsonElement PropItemOverrideArray(string description) => JsonSerializer.SerializeToElement(new
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
            },
            required = new[] { "index", "included" },
        },
    });

    private static JsonElement PropExperienceOverrideArray() => JsonSerializer.SerializeToElement(new
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
                company_description_override = new { type = "string", description = "Only if CV_BASE's wording differs from BACKGROUND's; omit/null otherwise." },
                achievements = PropItemOverrideArray("Only achievements whose CV_BASE wording differs, or that are absent — omit ones that match as-is."),
                extra_achievements = new { type = "array", items = new { type = "string" }, description = "CV_BASE bullets for this role with no BACKGROUND source." },
                notes = new { type = "string" },
            },
            required = new[] { "experience_index", "included", "achievements", "extra_achievements" },
        },
    });

    private static JsonElement PropSkillsSectionArray() => JsonSerializer.SerializeToElement(new
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

    private static JsonElement PropProjectOverrideArray() => JsonSerializer.SerializeToElement(new
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
                highlights = PropItemOverrideArray("Only highlights whose CV_BASE wording differs, or that are absent — omit ones that match as-is."),
                extra_highlights = new { type = "array", items = new { type = "string" } },
            },
            required = new[] { "project_index", "included", "highlights", "extra_highlights" },
        },
    });
}
