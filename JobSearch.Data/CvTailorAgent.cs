using System.Text;
using Anthropic;
using Anthropic.Models.Messages;

namespace JobSearch.Data;

public class CvTailorAgent
{
    private readonly AnthropicClient _client;
    private const string OpusModel = "claude-opus-4-8";
    private readonly string _skillText;
    private readonly ClaudeUsageLogger? _usageLogger;

    public CvTailorAgent(string apiKey, ClaudeUsageLogger? usageLogger = null)
    {
        _client = new AnthropicClient { ApiKey = apiKey };
        _skillText = SkillLoader.Load("tailor_cv.md");
        _usageLogger = usageLogger;
    }

    // Per-call, not per-instance: this agent is a DI singleton shared by every request, but
    // the candidate's background/CV base is per-user data (UserProfile), not something that
    // can be baked into the prompt once at construction.
    private string BuildSystemPrompt(UserProfile profile) => $"""
        {_skillText}

        --- CANDIDATE BACKGROUND ---
        {profile.Background}

        --- BASE CV ---
        {profile.CvBase}
        """;

    public static string BuildInitialUserContent(string postingText, string evaluationJson) => $"""
        Job posting:
        {postingText}

        --- EVALUATION ---
        {evaluationJson}
        """;

    public async Task<string> GenerateAsync(UserProfile profile, string postingText, string evaluationJson)
    {
        var userContent = BuildInitialUserContent(postingText, evaluationJson);

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = OpusModel,
            MaxTokens = 4000,
            System = new List<TextBlockParam>
            {
                new() { Text = BuildSystemPrompt(profile), CacheControl = new CacheControlEphemeral() },
            },
            Messages = [new() { Role = Role.User, Content = userContent }],
        });

        if (_usageLogger is not null)
            await _usageLogger.LogAsync(profile.UserId, ClaudeAgentName.CvTailorAgent, OpusModel, response.Usage);

        return ExtractText(response.Content);
    }

    public async Task<string> ReviseAsync(UserProfile profile, IReadOnlyList<AgentThreadTurn> history)
    {
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = OpusModel,
            MaxTokens = 4000,
            System = new List<TextBlockParam>
            {
                new() { Text = BuildSystemPrompt(profile), CacheControl = new CacheControlEphemeral() },
            },
            Messages = history.ToMessages(),
        });

        if (_usageLogger is not null)
            await _usageLogger.LogAsync(profile.UserId, ClaudeAgentName.CvTailorAgent, OpusModel, response.Usage);

        return ExtractText(response.Content);
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
