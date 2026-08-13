using Anthropic;
using Anthropic.Models.Messages;

namespace JobSearch.Data;

public class CoverLetterAgent
{
    private readonly AnthropicClient _client;
    private const string OpusModel = "claude-opus-4-8";
    private readonly string _skillText;

    public CoverLetterAgent(string apiKey)
    {
        _client = new AnthropicClient { ApiKey = apiKey };
        _skillText = SkillLoader.Load("write_cover_letter.md");
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
            Model = OpusModel,
            MaxTokens = 2048,
            System = new List<TextBlockParam>
            {
                new() { Text = BuildSystemPrompt(profile), CacheControl = new CacheControlEphemeral() },
            },
            Messages = [new() { Role = Role.User, Content = userContent }],
        });

        return ExtractText(response.Content);
    }

    public async Task<string> ReviseAsync(UserProfile profile, IReadOnlyList<AgentThreadTurn> history)
    {
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = OpusModel,
            MaxTokens = 2048,
            System = new List<TextBlockParam>
            {
                new() { Text = BuildSystemPrompt(profile), CacheControl = new CacheControlEphemeral() },
            },
            Messages = history.ToMessages(),
        });

        return ExtractText(response.Content);
    }

    private static string ExtractText(IReadOnlyList<ContentBlock> blocks) =>
        string.Concat(blocks.Select(b => b.TryPickText(out TextBlock? tb) ? tb?.Text ?? "" : "")).Trim();
}
