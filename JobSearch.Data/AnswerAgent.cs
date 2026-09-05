using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace JobSearch.Data;

public class AnswerAgent
{
    private readonly AnthropicClient _client;
    private const string SonnetModel = "claude-sonnet-5";

    private readonly string _skillText;
    private readonly Tool _tool;
    private readonly ClaudeUsageLogger? _usageLogger;

    public AnswerAgent(string apiKey, ClaudeUsageLogger? usageLogger = null)
    {
        _client = new AnthropicClient { ApiKey = apiKey };
        _skillText = SkillLoader.Load("answer_application_question.md");
        _usageLogger = usageLogger;

        _tool = new Tool
        {
            Name = "respond_to_candidate",
            Description = "Either ask one clarifying question, or give the final answer.",
            InputSchema = new InputSchema
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["mode"]    = PropEnum("Whether this is a follow-up question or the final answer.",
                                            "ask_followup", "final_answer"),
                    ["content"] = Prop("string", "The clarifying question, or the final answer text."),
                },
                Required = ["mode", "content"],
            },
            CacheControl = new CacheControlEphemeral(),
        };
    }

    // Per-call, not per-instance — see CvTailorAgent.BuildSystemPrompt for why.
    private string BuildSystemPrompt(UserProfile profile) => $"""
        {_skillText}

        --- CANDIDATE BACKGROUND ---
        {profile.Background}
        """;

    public static string BuildInitialUserContent(string question, string? jobContext) => jobContext is not null
        ? $"""
          Application question: {question}

          --- JOB CONTEXT ---
          {jobContext}
          """
        : $"Application question: {question}";

    public Task<(string Mode, string Content)> RespondAsync(UserProfile profile, IReadOnlyList<AgentThreadTurn> history) =>
        ClaudeToolCallRetry.CallAsync(
            _client,
            buildRequest: messages => new MessageCreateParams
            {
                Model = SonnetModel,
                MaxTokens = 1024,
                System = new List<TextBlockParam>
                {
                    new() { Text = BuildSystemPrompt(profile), CacheControl = new CacheControlEphemeral() },
                },
                Tools = [_tool],
                ToolChoice = new ToolChoiceAny(),
                Messages = [.. messages],
            },
            initialMessages: history.ToMessages(),
            toolName: _tool.Name,
            parse: i => (i["mode"].GetString() ?? "final_answer", i["content"].GetString() ?? ""),
            missingToolUseMessage: "Answer agent did not return a tool use block.",
            logLabel: nameof(AnswerAgent),
            onUsage: _usageLogger is null ? null : usage => _usageLogger.LogAsync(profile.UserId, ClaudeAgentName.AnswerAgent, SonnetModel, usage));

    private static JsonElement Prop(string type, string description) =>
        JsonSerializer.SerializeToElement(new { type, description });

    private static JsonElement PropEnum(string description, params string[] values) =>
        JsonSerializer.SerializeToElement(new { type = "string", description, @enum = values });
}
