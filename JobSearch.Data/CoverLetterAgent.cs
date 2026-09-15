using Anthropic;
using Anthropic.Models.Messages;

namespace JobSearch.Data;

public class CoverLetterAgent
{
    private readonly AnthropicClient _client;
    // Reverted from Sonnet alongside CvTailorAgent (2026-08-19) — this incident's worst example
    // was a cover letter whose entire generated content was the word "To". Renamed from the
    // stale "SonnetModel" name (misleading — it holds the Opus id) during a later investigation
    // into whether this is now safe to re-test: call shape here is UNCHANGED since the incident
    // (still one free-text call, MaxTokens=2048, no tool-use, no stop_reason or output-length
    // check on the response before it's persisted/shown), and no PR/issue/log record beyond the
    // revert commit message itself was found describing the actual cause (no stop_reason was ever
    // captured for the real incident either — nothing to check retroactively). Root cause not
    // identified with confidence, so left on Opus rather than guessing toward Sonnet. Before
    // revisiting, this call should at minimum log response.StopReason and content length so a
    // repeat is actually diagnosable, and only then re-test with the same failure-mode-focused
    // methodology used for CvTailorAgent (see CvTailorAgentModelEvalTests).
    private const string OpusModel = "claude-opus-4-8";
    private readonly string _skillText;
    private readonly string _skillVersion;
    private readonly ClaudeUsageLogger? _usageLogger;

    public CoverLetterAgent(string apiKey, ClaudeUsageLogger? usageLogger = null)
    {
        _client = new AnthropicClient { ApiKey = apiKey };
        _skillText = SkillLoader.Load("write_cover_letter.md");
        _skillVersion = SkillLoader.Version(_skillText);
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
            Model = OpusModel,
            MaxTokens = 2048,
            System = new List<TextBlockParam>
            {
                new() { Text = BuildSystemPrompt(profile), CacheControl = new CacheControlEphemeral() },
            },
            Messages = [new() { Role = Role.User, Content = userContent }],
        });

        if (_usageLogger is not null)
            await _usageLogger.LogAsync(profile.UserId, ClaudeAgentName.CoverLetterAgent, OpusModel, response.Usage, _skillVersion);

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

        if (_usageLogger is not null)
            await _usageLogger.LogAsync(profile.UserId, ClaudeAgentName.CoverLetterAgent, OpusModel, response.Usage, _skillVersion);

        return ExtractText(response.Content);
    }

    private static string ExtractText(IReadOnlyList<ContentBlock> blocks) =>
        string.Concat(blocks.Select(b => b.TryPickText(out TextBlock? tb) ? tb?.Text ?? "" : "")).Trim();
}
