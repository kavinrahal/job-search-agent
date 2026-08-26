using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace JobSearch.Data;

// Generates a fresh resume Summary from BACKGROUND alone (+ light steering from the candidate's
// target job titles), with no job posting in context — the Resume Builder page's "Generate
// summary" button. Distinct from ResumeBackfillAgent (reconciles an *existing* CvBase against
// BACKGROUND, a one-time migration tool — nothing to reconcile here) and CvTailorAgent (tailors
// to a specific posting, per-application — this is posting-agnostic). One tool-use call, same
// pattern as both, since a single short string field has no truncation-budget reason to split.
public class ResumeSummaryAgent
{
    private readonly AnthropicClient _client;
    private const string SonnetModel = "claude-sonnet-5";
    private const int MaxTokens = 1000;
    private readonly string _skillText;
    private readonly ClaudeUsageLogger? _usageLogger;
    private readonly Tool _summaryTool;

    public ResumeSummaryAgent(string apiKey, ClaudeUsageLogger? usageLogger = null)
    {
        _client = new AnthropicClient { ApiKey = apiKey };
        _skillText = SkillLoader.Load("generate_resume_summary.md");
        _usageLogger = usageLogger;

        _summaryTool = new Tool
        {
            Name = "submit_summary",
            Description = "Submit the freshly written resume summary.",
            InputSchema = new InputSchema
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["summary"] = ResumeOverrideSchema.Prop("string", "A fresh 2-4 sentence professional summary grounded in BACKGROUND, lightly steered by TARGET JOB TITLES when given."),
                },
                Required = ["summary"],
            },
            CacheControl = new CacheControlEphemeral(),
        };
    }

    // Background is not essentially empty here means at least one of these lists has content —
    // the resume-builder page's cue that there's actually something to summarize. Personal info
    // alone (name/email/location) isn't summary material. Kept as a static, pure check so the
    // "near-empty background" endpoint guard is testable without a live API call, same principle
    // as CvTailorAgent.ApplyDeltaAndRender being split out for the same reason.
    public static bool IsBackgroundEssentiallyEmpty(BackgroundData background) =>
        background.Experience.Count == 0 &&
        background.Education.Count == 0 &&
        background.Projects.Count == 0 &&
        background.Credentials.Count == 0 &&
        background.Publications.Count == 0 &&
        background.Volunteering.Count == 0;

    private static string BuildUserContent(string backgroundYaml, IReadOnlyList<string> targetJobTitles) => $"""
        --- BACKGROUND ---
        {backgroundYaml}

        --- TARGET JOB TITLES ---
        {(targetJobTitles.Count > 0 ? string.Join(", ", targetJobTitles) : "(none specified — write a generic, role-agnostic summary)")}
        """;

    public async Task<string> GenerateAsync(int userId, string backgroundYaml, IReadOnlyList<string> targetJobTitles)
    {
        var userContent = BuildUserContent(backgroundYaml, targetJobTitles);

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = SonnetModel,
            MaxTokens = MaxTokens,
            System = new List<TextBlockParam>
            {
                new() { Text = _skillText, CacheControl = new CacheControlEphemeral() },
            },
            Tools = [_summaryTool],
            ToolChoice = new ToolChoiceAny(),
            Messages = [new() { Role = Role.User, Content = userContent }],
        });

        if (_usageLogger is not null)
            await _usageLogger.LogAsync(userId, ClaudeAgentName.ResumeSummaryAgent, SonnetModel, response.Usage);

        foreach (var block in response.Content)
            if (block.TryPickToolUse(out ToolUseBlock? toolUse))
                return toolUse.Input.TryGetValue("summary", out var s) ? s.GetString() ?? "" : "";

        throw new InvalidOperationException("Resume summary generation did not return a tool use block for \"submit_summary\".");
    }
}
