using Anthropic;
using Anthropic.Models.Messages;

namespace JobSearch.Data;

public class CoverLetterAgent
{
    private readonly AnthropicClient _client;
    // Reverted from Sonnet alongside CvTailorAgent — see the comment there. This incident's
    // worst example was a cover letter whose entire generated content was the word "To".
    private const string SonnetModel = "claude-opus-4-8";
    private readonly string _skillText;
    private readonly ClaudeUsageLogger? _usageLogger;

    public CoverLetterAgent(string apiKey, ClaudeUsageLogger? usageLogger = null)
    {
        _client = new AnthropicClient { ApiKey = apiKey };
        _skillText = SkillLoader.Load("write_cover_letter.md");
        _usageLogger = usageLogger;
    }

    // Per-call, not per-instance — see CvTailorAgent.BuildSystemPrompt for why.
    private string BuildSystemPrompt(UserProfile profile) => $"""
        {_skillText}

        --- CANDIDATE BACKGROUND ---
        {profile.Background}
        """;

    public static string BuildInitialUserContent(string postingText, string evaluationJson) => $"""
        Job posting:
        {postingText}

        --- EVALUATION ---
        {evaluationJson}
        """;

    public async Task<string> GenerateAsync(UserProfile profile, string postingText, string evaluationJson, string? instruction = null)
    {
        string userContent = instruction is not null
            ? $"""
              {BuildInitialUserContent(postingText, evaluationJson)}

              Candidate instruction: {instruction}
              """
            : BuildInitialUserContent(postingText, evaluationJson);

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = SonnetModel,
            MaxTokens = 2048,
            System = new List<TextBlockParam>
            {
                new() { Text = BuildSystemPrompt(profile), CacheControl = new CacheControlEphemeral() },
            },
            Messages = [new() { Role = Role.User, Content = userContent }],
        });

        if (_usageLogger is not null)
            await _usageLogger.LogAsync(profile.UserId, ClaudeAgentName.CoverLetterAgent, SonnetModel, response.Usage);

        return ExtractText(response.Content);
    }

    public async Task<string> ReviseAsync(UserProfile profile, IReadOnlyList<AgentThreadTurn> history)
    {
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = SonnetModel,
            MaxTokens = 2048,
            System = new List<TextBlockParam>
            {
                new() { Text = BuildSystemPrompt(profile), CacheControl = new CacheControlEphemeral() },
            },
            Messages = history.ToMessages(),
        });

        if (_usageLogger is not null)
            await _usageLogger.LogAsync(profile.UserId, ClaudeAgentName.CoverLetterAgent, SonnetModel, response.Usage);

        return ExtractText(response.Content);
    }

    private static string ExtractText(IReadOnlyList<ContentBlock> blocks) =>
        string.Concat(blocks.Select(b => b.TryPickText(out TextBlock? tb) ? tb?.Text ?? "" : "")).Trim();
}
