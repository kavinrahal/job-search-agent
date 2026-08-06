using Anthropic;
using Anthropic.Models.Messages;

namespace JobSearch.Data;

public class CoverLetterAgent
{
    private readonly AnthropicClient _client;
    private const string OpusModel = "claude-opus-4-8";
    private readonly string _systemPrompt;

    public CoverLetterAgent(string apiKey)
    {
        _client = new AnthropicClient { ApiKey = apiKey };

        string skillText    = SkillLoader.Load("write_cover_letter.md");
        string background   = SkillLoader.Load("context/background.yaml");

        _systemPrompt = $"""
            {skillText}

            --- CANDIDATE BACKGROUND ---
            {background}
            """;
    }

    public static string BuildInitialUserContent(string postingText, string evaluationJson) => $"""
        Job posting:
        {postingText}

        --- EVALUATION ---
        {evaluationJson}
        """;

    public async Task<string> GenerateAsync(string postingText, string evaluationJson, string? instruction = null)
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
                new() { Text = _systemPrompt, CacheControl = new CacheControlEphemeral() },
            },
            Messages = [new() { Role = Role.User, Content = userContent }],
        });

        return ExtractText(response.Content);
    }

    public async Task<string> ReviseAsync(IReadOnlyList<AgentThreadTurn> history)
    {
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = OpusModel,
            MaxTokens = 2048,
            System = new List<TextBlockParam>
            {
                new() { Text = _systemPrompt, CacheControl = new CacheControlEphemeral() },
            },
            Messages = history.ToMessages(),
        });

        return ExtractText(response.Content);
    }

    private static string ExtractText(IReadOnlyList<ContentBlock> blocks) =>
        string.Concat(blocks.Select(b => b.TryPickText(out TextBlock? tb) ? tb?.Text ?? "" : "")).Trim();
}
