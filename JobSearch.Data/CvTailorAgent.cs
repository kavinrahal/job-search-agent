using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace JobSearch.Data;

// Tailors a candidate's structured resume (Background + UserResume) for a specific job
// application. GenerateAsync fires 3 parallel Claude tool-use calls instead of one free-text
// call — the model emits only what changes for this posting (a delta), not the entire document,
// which is the mechanism for the token-cost reduction this rearchitecture was partly motivated
// by: the old flow re-emitted the whole CV as output on every generation, most of it unchanged.
// The delta is applied on top of the base UserResume and rendered via the existing
// ResumeRenderer — GenerateAsync's public contract (Task<string>, the final markdown) is
// unchanged, so nothing downstream (AgentThread.CurrentContent, PDF rendering, the preview UI)
// needs to know tailoring works differently now.
public class CvTailorAgent
{
    private readonly AnthropicClient _client;
    // Same model as before the rearchitecture (see the historical Sonnet->Opus revert note this
    // replaced) — not revisited here, this change is about output shape, not model choice.
    private const string OpusModel = "claude-opus-4-8";
    private readonly string _skillText;
    private readonly ClaudeUsageLogger? _usageLogger;

    private readonly Tool _summarySkillsTool;
    private readonly Tool _experienceTool;
    private readonly Tool _projectsTool;

    // Summary+Skills: small, low truncation risk, merged into one call. Projects: matches
    // ResumeBackfillAgent's proven budget for a comparable-sized field. Experience: deliberately
    // NOT inherited from ResumeBackfillAgent's 4000 — that budget is proven for pure transcription
    // (backfill_resume.md explicitly forbids "improving" wording), but tailoring is a judgment
    // task across every role, and the *old* single-call free-text design hit a live truncation
    // incident at 4000 tokens on this same model doing a comparable-or-smaller job. Watch
    // production for a stop_reason of max_tokens on this call specifically; revisit if it recurs.
    private const int SummarySkillsMaxTokens = 2000;
    private const int ExperienceMaxTokens = 6000;
    private const int ProjectsMaxTokens = 4000;

    private const string TailoringNote = "Reorder or reword to fit this posting; omit an item only when it's genuinely irrelevant to this role.";

    public CvTailorAgent(string apiKey, ClaudeUsageLogger? usageLogger = null)
    {
        _client = new AnthropicClient { ApiKey = apiKey };
        _skillText = SkillLoader.Load("tailor_cv.md");
        _usageLogger = usageLogger;

        _summarySkillsTool = new Tool
        {
            Name = "submit_summary_and_skills",
            Description = "Submit a fresh Summary for this role and the reordered Skills section.",
            InputSchema = new InputSchema
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["summary"] = ResumeOverrideSchema.Prop("string", "Fresh 2-3 sentence summary specific to this role — never copy the current resume's summary verbatim."),
                    ["skills_section"] = ResumeOverrideSchema.PropSkillsSectionArray(),
                },
                Required = ["summary", "skills_section"],
            },
            CacheControl = new CacheControlEphemeral(),
        };

        _experienceTool = new Tool
        {
            Name = "submit_experience_overrides",
            Description = "Submit per-experience-entry tailoring for this specific posting.",
            InputSchema = new InputSchema
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["experience_overrides"] = ResumeOverrideSchema.PropExperienceOverrideArray(TailoringNote),
                },
                Required = ["experience_overrides"],
            },
            CacheControl = new CacheControlEphemeral(),
        };

        _projectsTool = new Tool
        {
            Name = "submit_project_overrides",
            Description = "Submit per-project tailoring for this specific posting.",
            InputSchema = new InputSchema
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["project_overrides"] = ResumeOverrideSchema.PropProjectOverrideArray(TailoringNote),
                },
                Required = ["project_overrides"],
            },
            CacheControl = new CacheControlEphemeral(),
        };
    }

    // Per-call, not per-instance: this agent is a DI singleton shared by every request, but the
    // candidate's background/resume is per-user data. background is passed in already-parsed
    // rather than re-parsed here, since callers need the parsed value again afterward (the final
    // render) and BackgroundYamlParser.Parse isn't free.
    private string BuildSystemPrompt(BackgroundData background, string backgroundYaml, UserResume resume) => $"""
        {_skillText}

        --- BACKGROUND ---
        {backgroundYaml}

        --- CURRENT RESUME ---
        {ResumeRenderer.Render(background, resume, isPromptContext: true)}
        """;

    public static string BuildInitialUserContent(string postingText, string evaluationJson) => $"""
        Job posting:
        {postingText}

        --- EVALUATION ---
        {evaluationJson}
        """;

    public async Task<string> GenerateAsync(UserProfile profile, UserResume resume, string postingText, string evaluationJson)
    {
        var background = BackgroundYamlParser.Parse(profile.Background);
        var systemPrompt = BuildSystemPrompt(background, profile.Background, resume);
        var userContent = BuildInitialUserContent(postingText, evaluationJson);

        var summarySkillsTask = CallAsync(profile.UserId, systemPrompt, userContent, _summarySkillsTool, SummarySkillsMaxTokens);
        var experienceTask = CallAsync(profile.UserId, systemPrompt, userContent, _experienceTool, ExperienceMaxTokens);
        var projectsTask = CallAsync(profile.UserId, systemPrompt, userContent, _projectsTool, ProjectsMaxTokens);
        await Task.WhenAll(summarySkillsTask, experienceTask, projectsTask);

        return ApplyDeltaAndRender(background, resume, summarySkillsTask.Result, experienceTask.Result, projectsTask.Result);
    }

    // Split out from GenerateAsync so the delta-combination logic is testable without a live API
    // call, same principle as ResumeIntakeAgent.ExtractField / ResumeBackfillAgent's extraction
    // methods — takes the three raw tool-use inputs directly.
    public static string ApplyDeltaAndRender(
        BackgroundData background, UserResume baseResume,
        IReadOnlyDictionary<string, JsonElement> summarySkillsInput,
        IReadOnlyDictionary<string, JsonElement> experienceInput,
        IReadOnlyDictionary<string, JsonElement> projectsInput)
    {
        var tailored = new UserResume
        {
            Summary = summarySkillsInput.TryGetValue("summary", out var s) ? s.GetString() ?? "" : "",
            // SectionConfig is deliberately copied unchanged, never tailored per-application —
            // tailor_cv.md has no rule that adds/removes a whole section, and its hard
            // constraints explicitly forbid restructuring the overall format or section order.
            SectionConfigJson = baseResume.SectionConfigJson,
            ExperienceOverridesJson = JsonSerializer.Serialize(ResumeOverrideSchema.ExtractExperienceOverrides(experienceInput, "experience_overrides")),
            SkillsSectionJson = JsonSerializer.Serialize(ResumeOverrideSchema.ExtractSkillsSection(summarySkillsInput, "skills_section")),
            ProjectOverridesJson = JsonSerializer.Serialize(ResumeOverrideSchema.ExtractProjectOverrides(projectsInput, "project_overrides")),
        };

        return ResumeRenderer.Render(background, tailored);
    }

    public async Task<string> ReviseAsync(UserProfile profile, UserResume resume, IReadOnlyList<AgentThreadTurn> history)
    {
        var background = BackgroundYamlParser.Parse(profile.Background);
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = OpusModel,
            MaxTokens = 4000,
            System = new List<TextBlockParam>
            {
                new() { Text = BuildSystemPrompt(background, profile.Background, resume), CacheControl = new CacheControlEphemeral() },
            },
            Messages = history.ToMessages(),
        });

        if (_usageLogger is not null)
            await _usageLogger.LogAsync(profile.UserId, ClaudeAgentName.CvTailorAgent, OpusModel, response.Usage);

        return ExtractText(response.Content);
    }

    private async Task<IReadOnlyDictionary<string, JsonElement>> CallAsync(int userId, string systemPrompt, string userContent, Tool tool, int maxTokens)
    {
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = OpusModel,
            MaxTokens = maxTokens,
            System = new List<TextBlockParam>
            {
                new() { Text = systemPrompt, CacheControl = new CacheControlEphemeral() },
            },
            Tools = [tool],
            ToolChoice = new ToolChoiceAny(),
            Messages = [new() { Role = Role.User, Content = userContent }],
        });

        if (_usageLogger is not null)
            await _usageLogger.LogAsync(userId, ClaudeAgentName.CvTailorAgent, OpusModel, response.Usage);

        foreach (var block in response.Content)
            if (block.TryPickToolUse(out ToolUseBlock? toolUse))
                return toolUse.Input;

        throw new InvalidOperationException($"CV tailoring did not return a tool use block for \"{tool.Name}\".");
    }

    private static string ExtractText(IReadOnlyList<ContentBlock> blocks)
    {
        var sb = new StringBuilder();
        foreach (var block in blocks)
            if (block.TryPickText(out TextBlock? tb) && tb is not null)
                sb.Append(tb.Text);
        return sb.ToString().Trim();
    }
}
