using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using JobSearch.Data;
using JobSearchAgent.Integrations;

namespace JobSearchAgent.Agents;

public class PostingMatcherAgent
{
    private readonly AnthropicClient _client;
    private const string HaikuModel = "claude-haiku-4-5";

    private readonly string _systemPrompt;
    private readonly Tool _tool;
    private readonly ClaudeUsageLogger? _usageLogger;

    public PostingMatcherAgent(string apiKey, ClaudeUsageLogger? usageLogger = null)
    {
        _client = new AnthropicClient { ApiKey = apiKey };
        _usageLogger = usageLogger;
        _systemPrompt = SkillLoader.Load("match_posting.md");

        _tool = new Tool
        {
            Name = "pick_match",
            Description = "Identify which candidate posting, if any, is confidently the same job as the target.",
            InputSchema = new InputSchema
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["matched_index"] = JsonSerializer.SerializeToElement(new
                    {
                        type = "integer",
                        description = "0-based index into the candidate list that is confidently the same job. Omit entirely if none confidently match.",
                    }),
                },
                Required = [],
            },
            CacheControl = new CacheControlEphemeral(),
        };
    }

    protected PostingMatcherAgent() { _client = null!; _systemPrompt = ""; _tool = null!; _usageLogger = null; }

    // targetContext is a short raw excerpt from the alert email around the job's mention
    // (title/company/location/salary, in whatever order the email happened to list them) —
    // deliberately not pre-parsed into fields, since letting the model read the messy
    // original text is more robust than a hand-rolled parser tied to one email template.
    public virtual async Task<JobFeedItem?> FindMatchAsync(
        int userId, string targetContext, IReadOnlyList<JobFeedItem> candidates)
    {
        if (candidates.Count == 0) return null;

        var candidateList = string.Join("\n\n", candidates.Select((c, i) =>
            $"[{i}] {c.Title} — {c.Company} — {c.Location}"));

        string userContent = $"""
            Target job, from an alert email (the posting page itself couldn't be fetched):
            {targetContext}

            Candidate postings found via search:
            {candidateList}
            """;

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = HaikuModel,
            MaxTokens = 256,
            System = new List<TextBlockParam>
            {
                new() { Text = _systemPrompt, CacheControl = new CacheControlEphemeral() },
            },
            Tools = [_tool],
            ToolChoice = new ToolChoiceAny(),
            Messages = [new() { Role = Role.User, Content = userContent }],
        });

        if (_usageLogger is not null)
            await _usageLogger.LogAsync(userId, ClaudeAgentName.PostingMatcherAgent, HaikuModel, response.Usage);

        foreach (var block in response.Content)
        {
            if (block.TryPickToolUse(out ToolUseBlock? toolUse) &&
                toolUse.Input.TryGetValue("matched_index", out var idx) &&
                idx.ValueKind == JsonValueKind.Number &&
                idx.TryGetInt32(out var i) &&
                i >= 0 && i < candidates.Count)
            {
                return candidates[i];
            }
        }

        return null;
    }
}
