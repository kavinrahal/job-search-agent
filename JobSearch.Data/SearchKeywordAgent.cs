using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace JobSearch.Data;

// Derives job-board search keywords (Adzuna's free-text "what" query) from a candidate's own
// job criteria — proactive discovery has to work for any profession this app serves, not just
// software engineering, so the search terms can't be a fixed list. See AdzunaFetcher's comment
// on why it takes no hardcoded default.
public class SearchKeywordAgent
{
    private readonly AnthropicClient _client;
    private const string HaikuModel = "claude-haiku-4-5";
    private readonly Tool _tool;
    private readonly ClaudeUsageLogger? _usageLogger;

    public SearchKeywordAgent(string apiKey, ClaudeUsageLogger? usageLogger = null)
    {
        _client = new AnthropicClient { ApiKey = apiKey };
        _usageLogger = usageLogger;

        _tool = new Tool
        {
            Name = "search_keywords",
            Description = "Produce job-board search query strings for this candidate's target roles.",
            InputSchema = new InputSchema
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["keywords"] = JsonSerializer.SerializeToElement(new
                    {
                        type = "array",
                        description =
                            "3 to 6 short search strings (2-4 words each) that a job board's free-text " +
                            "search would use to find roles matching this candidate — e.g. \"sous chef\", " +
                            "\"backend developer\", \"registered nurse\". Base these on the role titles and " +
                            "skill dimensions actually named in the candidate's criteria, not assumptions.",
                        items = new { type = "string" },
                        minItems = 3,
                        maxItems = 6,
                    }),
                },
                Required = ["keywords"],
            },
            CacheControl = new CacheControlEphemeral(),
        };
    }

    protected SearchKeywordAgent() { _client = null!; _tool = null!; _usageLogger = null; }

    public virtual async Task<string[]> GenerateAsync(int userId, string jobCriteria)
    {
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = HaikuModel,
            MaxTokens = 256,
            Tools = [_tool],
            ToolChoice = new ToolChoiceAny(),
            Messages = [new() { Role = Role.User, Content = $"Candidate's job criteria:\n\n{jobCriteria}" }],
        });

        if (_usageLogger is not null)
            await _usageLogger.LogAsync(userId, ClaudeAgentName.SearchKeywordAgent, HaikuModel, response.Usage);

        foreach (var block in response.Content)
        {
            if (block.TryPickToolUse(out ToolUseBlock? toolUse) &&
                toolUse.Input.TryGetValue("keywords", out var k) &&
                k.ValueKind == JsonValueKind.Array)
            {
                return [.. k.EnumerateArray()
                    .Select(e => e.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!)];
            }
        }
        return [];
    }
}
