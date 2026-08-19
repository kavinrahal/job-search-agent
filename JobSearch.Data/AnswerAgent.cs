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

    public async Task<(string Mode, string Content)> RespondAsync(UserProfile profile, IReadOnlyList<AgentThreadTurn> history)
    {
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = SonnetModel,
            MaxTokens = 1024,
            System = new List<TextBlockParam>
            {
                new() { Text = BuildSystemPrompt(profile), CacheControl = new CacheControlEphemeral() },
            },
            Tools = [_tool],
            ToolChoice = new ToolChoiceAny(),
            Messages = history.ToMessages(),
        });

        if (_usageLogger is not null)
            await _usageLogger.LogAsync(profile.UserId, ClaudeAgentName.AnswerAgent, SonnetModel, response.Usage);

        foreach (var block in response.Content)
        {
            if (block.TryPickToolUse(out ToolUseBlock? toolUse))
            {
                var i = toolUse.Input;
                return (i["mode"].GetString() ?? "final_answer", i["content"].GetString() ?? "");
            }
        }

        throw new InvalidOperationException("Answer agent did not return a tool use block.");
    }

    private static JsonElement Prop(string type, string description) =>
        JsonSerializer.SerializeToElement(new { type, description });

    private static JsonElement PropEnum(string description, params string[] values) =>
        JsonSerializer.SerializeToElement(new { type = "string", description, @enum = values });
}
