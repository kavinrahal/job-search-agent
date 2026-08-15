using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace JobSearch.Data;

// Used to name downloaded CV/cover-letter files ("{Applicant} - {Company} - Resume.pdf").
// Most generations (paste a URL or text, the common Generate-tab path) never run a full
// PostingEvaluator pass, so company name isn't otherwise known — this is a cheap, single-field
// extraction rather than reusing the full evaluator for a value it happens to also produce.
public class CompanyExtractorAgent
{
    private readonly AnthropicClient _client;
    private const string HaikuModel = "claude-haiku-4-5";
    private readonly Tool _tool;
    private readonly ClaudeUsageLogger? _usageLogger;

    public CompanyExtractorAgent(string apiKey, ClaudeUsageLogger? usageLogger = null)
    {
        _client = new AnthropicClient { ApiKey = apiKey };
        _usageLogger = usageLogger;

        _tool = new Tool
        {
            Name = "extract_company",
            Description = "Extract the hiring company's name from job posting text.",
            InputSchema = new InputSchema
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["company"] = JsonSerializer.SerializeToElement(new
                    {
                        type = "string",
                        description = "The hiring company's name. Omit this field entirely if it isn't identifiable from the text — do not guess.",
                    }),
                },
                Required = [],
            },
            CacheControl = new CacheControlEphemeral(),
        };
    }

    protected CompanyExtractorAgent() { _client = null!; _tool = null!; _usageLogger = null; }

    public virtual async Task<string?> ExtractAsync(int userId, string postingText)
    {
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = HaikuModel,
            MaxTokens = 64,
            Tools = [_tool],
            ToolChoice = new ToolChoiceAny(),
            Messages = [new() { Role = Role.User, Content = $"Job posting text:\n\n{Truncate(postingText, 2000)}" }],
        });

        if (_usageLogger is not null)
            await _usageLogger.LogAsync(userId, ClaudeAgentName.CompanyExtractorAgent, HaikuModel, response.Usage);

        foreach (var block in response.Content)
        {
            if (block.TryPickToolUse(out ToolUseBlock? toolUse) &&
                toolUse.Input.TryGetValue("company", out var c) &&
                c.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(c.GetString()))
            {
                return c.GetString();
            }
        }
        return null;
    }

    private static string Truncate(string text, int maxChars) => text.Length <= maxChars ? text : text[..maxChars];
}
