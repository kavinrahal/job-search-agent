using System.Text;
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

    public async Task<string> GenerateAsync(string postingText, string evaluationJson, string? instruction = null)
    {
        var content = new StringBuilder();
        content.AppendLine("Job posting:");
        content.AppendLine(postingText);
        content.AppendLine();
        content.AppendLine("--- EVALUATION ---");
        content.AppendLine(evaluationJson);

        if (instruction is not null)
        {
            content.AppendLine();
            content.AppendLine($"Candidate instruction: {instruction}");
        }

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = OpusModel,
            MaxTokens = 2048,
            System = new List<TextBlockParam>
            {
                new() { Text = _systemPrompt, CacheControl = new CacheControlEphemeral() },
            },
            Messages = [new() { Role = Role.User, Content = content.ToString() }],
        });

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
