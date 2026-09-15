using Anthropic;
using Anthropic.Models.Messages;

namespace JobSearch.Data;

public class CoverLetterAgent
{
    private readonly AnthropicClient _client;
    // Reverted from Sonnet alongside CvTailorAgent (2026-08-19) — this incident's worst example
    // was a cover letter whose entire generated content was the word "To". Renamed from the
    // stale "SonnetModel" name (misleading — it holds the Opus id) during a later investigation
    // into whether this is now safe to re-test. Root cause was never identified with confidence
    // (no stop_reason was ever captured for the real incident — nothing to check retroactively),
    // so this stays on Opus rather than guessing toward Sonnet. The safeguard below (see
    // CallWithSafeguardAsync) closes the gap the investigation actually surfaced: the failure
    // that reached a real user wasn't fundamentally about the model, it was that this call had no
    // way to catch a truncated/degenerate response before persisting it. That check is now in
    // place and model-independent, and CoverLetterAgentModelEvalTests re-runs the CvTailorAgent
    // methodology against Sonnet *with the safeguard active* — switch this default to Sonnet only
    // if that eval comes back clean and the safeguard didn't have to paper over a bad response.
    private const string OpusModel = "claude-opus-4-8";
    // Overridable via the constructor, defaulting to OpusModel — exists so the regression eval can
    // exercise this agent's real call shape (including the safeguard) against a different model
    // without a second copy of this class. See CoverLetterAgentModelEvalTests. Both call sites
    // read _model, never OpusModel directly, so an override takes effect everywhere.
    private readonly string _model;
    private readonly string _skillText;
    private readonly string _skillVersion;
    private readonly ClaudeUsageLogger? _usageLogger;

    private const int MaxTokens = 2048;
    // The original call plus exactly one retry — matches ClaudeToolCallRetry's convention (a fixed
    // constant, no backoff, no configurable knob; see that class and CLAUDE.md).
    private const int MaxAttempts = 2;

    // model: test-only override (see _model above); every production call site omits it and gets
    // OpusModel.
    public CoverLetterAgent(string apiKey, ClaudeUsageLogger? usageLogger = null, string? model = null)
    {
        _client = new AnthropicClient { ApiKey = apiKey };
        _skillText = SkillLoader.Load("write_cover_letter.md");
        _skillVersion = SkillLoader.Version(_skillText);
        _usageLogger = usageLogger;
        _model = model ?? OpusModel;
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

        return await CallWithSafeguardAsync(profile, [new() { Role = Role.User, Content = userContent }]);
    }

    public async Task<string> ReviseAsync(UserProfile profile, IReadOnlyList<AgentThreadTurn> history) =>
        await CallWithSafeguardAsync(profile, history.ToMessages());

    // Shared call path for both GenerateAsync and ReviseAsync (they differ only in the message
    // list). A cover letter is a free-text call with no schema to force, so a degenerate response
    // — the truncated/one-word shape the 2026-08-19 incident took ("To" as an entire letter) —
    // comes back as an ordinary 200, not the thrown exception a missing tool-use block would give
    // (see CoverLetterOutputValidator's comment). Left unchecked it was persisted and served as if
    // it were a real letter. This retries once on that shape, on the assumption (same as
    // ClaudeToolCallRetry's) that it's usually a one-off model hiccup a fresh generation recovers.
    // A plain re-issue of the same request, not a corrective replay turn: for a free-text
    // generation there's nothing to correct back to the model — a truncation or degenerate output
    // is a sampling artifact, and replaying it would only risk compounding it.
    private async Task<string> CallWithSafeguardAsync(UserProfile profile, IReadOnlyList<MessageParam> messages)
    {
        var systemPrompt = BuildSystemPrompt(profile);
        string text = "";

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = _model,
                MaxTokens = MaxTokens,
                System = new List<TextBlockParam>
                {
                    new() { Text = systemPrompt, CacheControl = new CacheControlEphemeral() },
                },
                Messages = [.. messages],
            });

            // Every attempt is a billed call — log each one's usage, same as ClaudeToolCallRetry.
            if (_usageLogger is not null)
                await _usageLogger.LogAsync(profile.UserId, ClaudeAgentName.CoverLetterAgent, _model, response.Usage, _skillVersion);

            text = ExtractText(response.Content);

            if (!LooksTruncatedOrDegenerate(response, text, out var diagnostic))
                return text;

            // Log stop_reason + length whether we retry or give up: the investigation into the
            // original incident found nothing was ever recorded about it, so a repeat needs to be
            // diagnosable from the logs.
            bool isLastAttempt = attempt == MaxAttempts;
            await Console.Error.WriteLineAsync(isLastAttempt
                ? $"[CoverLetterAgent] response still failed the sanity check after {MaxAttempts} attempts ({diagnostic}) — returning it for downstream validation."
                : $"[CoverLetterAgent] response failed the sanity check on attempt {attempt} ({diagnostic}) — retrying once.");
        }

        // Final attempt's text, still suspect: return it rather than throw, so the authoritative
        // gate — CoverLetterOutputValidator at the API layer (Program.cs) — turns it into a clean
        // refund + 422 for the user instead of an exception. Same principle as ClaudeToolCallRetry
        // letting a still-bad shape surface its own downstream handling on the final attempt.
        return text;
    }

    // Deliberately narrower than CoverLetterOutputValidator.LooksLikeCoverLetter (the authoritative
    // API-layer gate): only the two transient shapes a re-run can plausibly fix — a truncated
    // generation (any terminal stop_reason other than end_turn, max_tokens being the one the
    // incident could have produced) and a suspiciously short body. Salutation/refusal failures are
    // left to that gate: re-running the same prompt won't fix a sparse-background refusal, so a
    // retry would just burn a second billed call before the same 422.
    private static bool LooksTruncatedOrDegenerate(Message response, string text, out string diagnostic)
    {
        // .Raw() is the wire value ("end_turn", "max_tokens", ...); comparing the string avoids
        // throwing on any stop_reason this SDK's enum doesn't know. Non-streaming responses always
        // carry one, so a null here is itself abnormal.
        var stopReason = response.StopReason?.Raw();
        var wordCount = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        diagnostic = $"stop_reason={stopReason ?? "null"}, words={wordCount}";

        bool abnormalStop = !string.Equals(stopReason, "end_turn", StringComparison.Ordinal);
        bool tooShort = wordCount < CoverLetterOutputValidator.MinWords;
        return abnormalStop || tooShort;
    }

    private static string ExtractText(IReadOnlyList<ContentBlock> blocks) =>
        string.Concat(blocks.Select(b => b.TryPickText(out TextBlock? tb) ? tb?.Text ?? "" : "")).Trim();
}
