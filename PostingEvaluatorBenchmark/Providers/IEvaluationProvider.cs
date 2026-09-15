using JobSearch.Data;

namespace PostingEvaluatorBenchmark.Providers;

// Token/cost accounting for one evaluation call, independent of which provider produced it.
// InputTokens/OutputTokens/CacheReadInputTokens/CacheCreationInputTokens follow Anthropic's own
// usage vocabulary (see JobSearch.Data.ClaudeUsageLog) -- a future non-Anthropic provider maps
// its own usage fields onto this same shape (0 for CacheRead/CacheCreation if that provider has
// no prompt-caching equivalent), so Pricing/ModelPricing.cs's cost formula stays provider-agnostic.
public record EvaluationUsage(
    string Provider,
    string Model,
    long InputTokens,
    long OutputTokens,
    long CacheReadInputTokens,
    long CacheCreationInputTokens);

// The result of one (criteria, posting) evaluation call, independent of which provider produced
// it -- Evaluation is PostingEvaluator's own real output DTO (not reimplemented; see
// ClaudeEvaluationProvider), so a future provider adapter must map its own structured output onto
// this exact same PostingEvaluation shape (recommendation/disqualifier_hit/per-dimension
// assessments) for Scoring/AccuracyScorer.cs to grade it identically regardless of provider.
public record ProviderEvaluationResult(PostingEvaluation Evaluation, EvaluationUsage Usage, TimeSpan Duration);

// Extension point for a model/provider under benchmark. Only ClaudeEvaluationProvider exists
// today (Sonnet 5 / Opus 4.8 / Haiku 4.5, selected via PostingEvaluator's test-only model
// override -- see JobSearch.Data/PostingEvaluator.cs). OpenAI/Gemini are NOT implemented here
// (no API keys are set up for them yet, per the task brief) -- deliberately no half-built,
// silently-failing stub for either. A future provider adapter needs to:
//   1. Make the real API call for that provider's equivalent of "evaluate this posting against
//      this candidate's criteria", using evaluate_posting.md's schema/semantics as the contract
//      (forced structured output, not free text -- see architecture-conventions.md's "Structured
//      output from Claude" section, which applies equally to any provider here).
//   2. Map that provider's response into PostingEvaluation (same field names/enum values
//      PostingEvaluator.ParseEvaluation produces) and its token usage into EvaluationUsage.
//   3. Register a cost row for that provider's model(s) in Pricing/ModelPricing.cs.
// No other harness code (fixtures, scoring, reporting) needs to change.
public interface IEvaluationProvider
{
    // e.g. "claude:claude-sonnet-5" -- used as the comparison table's row key.
    string Name { get; }
    string Model { get; }

    Task<ProviderEvaluationResult> EvaluateAsync(
        UserProfile profile, string postingText, string? sourceUrl, CancellationToken cancellationToken = default);
}
