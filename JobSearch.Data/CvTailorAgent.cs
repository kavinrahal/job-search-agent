using System.Text;
using Anthropic;
using Anthropic.Models.Messages;

namespace JobSearch.Data;

public class CvTailorAgent
{
    private readonly AnthropicClient _client;
    private const string OpusModel = "claude-opus-4-8";
    private readonly string _systemPrompt;

    public CvTailorAgent(string apiKey)
    {
        _client = new AnthropicClient { ApiKey = apiKey };

        string skillText  = SkillLoader.Load("tailor_cv.md");
        string background = SkillLoader.Load("context/background.yaml");
        string cvBase     = SkillLoader.Load("context/cv_base.md");

        _systemPrompt = $"""
            {skillText}

            --- CANDIDATE BACKGROUND ---
            {background}

            --- BASE CV ---
            {cvBase}
            """;
    }

    public async Task<string> GenerateAsync(string postingText, string evaluationJson)
    {
        var userContent = $"""
            Job posting:
            {postingText}

            --- EVALUATION ---
            {evaluationJson}
            """;

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = OpusModel,
            MaxTokens = 4000,
            System = new List<TextBlockParam>
            {
                new() { Text = _systemPrompt, CacheControl = new CacheControlEphemeral() },
            },
            Messages = [new() { Role = Role.User, Content = userContent }],
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
