using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace JobSearch.Data;

// Runs after every CV/cover-letter/answer generation and revision — a cheap second pass that
// checks the generated content against the candidate's own source material and flags any claim
// that isn't traceable back to it. Non-blocking by design: this doesn't retry or reject
// anything, it just surfaces what it finds so the user can review before submitting. The
// primary defense against fabrication is the generation skills' own instructions
// (tailor_cv.md's "you do not fabricate experience, tools, or metrics" etc.) — this is a
// verification layer on top, not a replacement for it.
public class AccuracyVerifierAgent
{
    private readonly AnthropicClient _client;
    private const string HaikuModel = "claude-haiku-4-5";
    private readonly string _skillText;
    private readonly Tool _tool;
    private readonly ClaudeUsageLogger? _usageLogger;

    public AccuracyVerifierAgent(string apiKey, ClaudeUsageLogger? usageLogger = null)
    {
        _client = new AnthropicClient { ApiKey = apiKey };
        _skillText = SkillLoader.Load("verify_accuracy.md");
        _usageLogger = usageLogger;

        _tool = new Tool
        {
            Name = "flag_unverified_claims",
            Description = "List any claims in the generated content that can't be traced back to the candidate's source material.",
            InputSchema = new InputSchema
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["flagged_claims"] = JsonSerializer.SerializeToElement(new
                    {
                        type = "array",
                        description =
                            "One entry per unverifiable claim (a specific tool, metric, company, date, " +
                            "responsibility, or outcome not found in the source material). Empty array if " +
                            "everything is traceable.",
                        items = new { type = "string" },
                    }),
                },
                Required = ["flagged_claims"],
            },
            CacheControl = new CacheControlEphemeral(),
        };
    }

    protected AccuracyVerifierAgent() { _client = null!; _skillText = ""; _tool = null!; _usageLogger = null; }

    public virtual async Task<string[]> VerifyAsync(int userId, string sourceMaterial, string generatedContent)
    {
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = HaikuModel,
            MaxTokens = 1024,
            // Verification against the candidate's own source material benefits from
            // consistency — low temperature instead of the API default (1.0). See
            // ClaudeTemperature. The SDK marks this property [Obsolete] because post-Opus-4.6
            // models (Sonnet 5, Opus 4.7/4.8) reject any non-default value with a 400 —
            // claude-haiku-4-5 predates that restriction and still honors it.
#pragma warning disable CS0618
            Temperature = ClaudeTemperature.Classification,
#pragma warning restore CS0618
            System = new List<TextBlockParam>
            {
                new() { Text = _skillText, CacheControl = new CacheControlEphemeral() },
            },
            Tools = [_tool],
            ToolChoice = new ToolChoiceAny(),
            Messages = [new()
            {
                Role = Role.User,
                Content = $"--- SOURCE MATERIAL ---\n{sourceMaterial}\n\n--- GENERATED CONTENT TO VERIFY ---\n{generatedContent}",
            }],
        });

        if (_usageLogger is not null)
            await _usageLogger.LogAsync(userId, ClaudeAgentName.AccuracyVerifierAgent, HaikuModel, response.Usage);

        foreach (var block in response.Content)
        {
            if (block.TryPickToolUse(out ToolUseBlock? toolUse))
                return ExtractFlaggedClaims(toolUse.Input);
        }
        return [];
    }

    // Pulled out of VerifyAsync so the parsing logic is testable without a live/mocked API
    // call — same reasoning as ResumeIntakeAgent.ExtractField.
    internal static string[] ExtractFlaggedClaims(IReadOnlyDictionary<string, JsonElement> toolInput)
    {
        if (!toolInput.TryGetValue("flagged_claims", out var claims) || claims.ValueKind != JsonValueKind.Array)
            return [];

        return [.. claims.EnumerateArray()
            .Select(e => e.GetString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!)];
    }
}
