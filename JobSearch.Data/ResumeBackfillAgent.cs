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
// Schema/parsing logic lives in ResumeOverrideSchema, shared with CvTailorAgent's per-application
// tailoring calls — same override shapes, different judgment rules populating them.
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

    private const string DivergenceNote = "Only where the wording differs from BACKGROUND, or is absent — omit ones that match as-is.";

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
                    ["summary"] = ResumeOverrideSchema.Prop("string", "CV_BASE's Summary section text, or empty string if it's only the placeholder."),
                    ["section_config"] = ResumeOverrideSchema.PropSectionConfigArray(),
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
                    ["experience_overrides"] = ResumeOverrideSchema.PropExperienceOverrideArray(DivergenceNote),
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
                    ["skills_section"] = ResumeOverrideSchema.PropSkillsSectionArray(),
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
                    ["project_overrides"] = ResumeOverrideSchema.PropProjectOverrideArray(DivergenceNote),
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
            SectionConfig: ResumeOverrideSchema.ExtractSectionConfig(summaryConfigInput, "section_config"),
            ExperienceOverrides: ResumeOverrideSchema.ExtractExperienceOverrides(experienceTask.Result, "experience_overrides"),
            SkillsSection: ResumeOverrideSchema.ExtractSkillsSection(skillsTask.Result, "skills_section"),
            ProjectOverrides: ResumeOverrideSchema.ExtractProjectOverrides(projectsTask.Result, "project_overrides"));
    }

    private Task<IReadOnlyDictionary<string, JsonElement>> CallAsync(int userId, string userContent, Tool tool) =>
        ClaudeToolCallRetry.CallAsync(
            _client,
            buildRequest: messages => new MessageCreateParams
            {
                Model = SonnetModel,
                MaxTokens = MaxTokens,
                System = new List<TextBlockParam>
                {
                    new() { Text = _skillText, CacheControl = new CacheControlEphemeral() },
                },
                Tools = [tool],
                ToolChoice = new ToolChoiceAny(),
                Messages = [.. messages],
            },
            initialMessages: [new() { Role = Role.User, Content = userContent }],
            toolName: tool.Name,
            parse: input => input,
            missingToolUseMessage: $"Resume backfill did not return a tool use block for \"{tool.Name}\".",
            logLabel: nameof(ResumeBackfillAgent),
            onUsage: _usageLogger is null ? null : usage => _usageLogger.LogAsync(userId, ClaudeAgentName.ResumeBackfillAgent, SonnetModel, usage));
}
