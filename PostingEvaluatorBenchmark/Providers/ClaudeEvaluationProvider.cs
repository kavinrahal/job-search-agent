using System.Diagnostics;
using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

namespace PostingEvaluatorBenchmark.Providers;

// The only real IEvaluationProvider today -- wraps PostingEvaluator's actual production call
// path (BuildSystemPrompt, the forced-tool-use schema, ClaudeToolCallRetry, ParseEvaluation) via
// its narrow, additive, test-only `model` constructor override (see PostingEvaluator.cs), so this
// harness is testing the real code every live evaluation runs through -- not a reimplementation.
//
// Usage capture: PostingEvaluator already threads real per-call Usage through to a
// ClaudeUsageLogger when one is supplied (see EvaluateAsync's onUsage callback and
// ClaudeUsageLogger.LogAsync). Rather than adding a second, benchmark-only usage-capture hook to
// PostingEvaluator's production surface, this provider points a ClaudeUsageLogger at a private,
// per-instance EF Core InMemory database (not Postgres -- no production data, nothing written to
// disk) and reads the row straight back out after each call. This is the same pattern
// JobSearchAgent.Tests/TestFixtures.cs's Db.Fresh() already uses for AppDbContext in tests.
// Sequential-only by design (matches the harness's own "sample-size/variety, not concurrency"
// brief -- see BenchmarkRunner) -- do not call EvaluateAsync concurrently on the same instance,
// since the "read the most recent row" lookup below assumes no interleaved writes.
public sealed class ClaudeEvaluationProvider : IEvaluationProvider
{
    private readonly PostingEvaluator _evaluator;
    private readonly DbContextOptions<AppDbContext> _usageDbOptions;

    public string Name => $"claude:{Model}";
    public string Model { get; }

    public ClaudeEvaluationProvider(string apiKey, string model)
    {
        Model = model;
        _usageDbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"posting-evaluator-benchmark-usage-{Guid.NewGuid()}")
            .Options;
        var usageLogger = new ClaudeUsageLogger(_usageDbOptions);
        _evaluator = new PostingEvaluator(apiKey, usageLogger, model);
    }

    public async Task<ProviderEvaluationResult> EvaluateAsync(
        UserProfile profile, string postingText, string? sourceUrl, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var evaluation = await _evaluator.EvaluateAsync(profile, postingText, sourceUrl);
        stopwatch.Stop();

        await using var db = new AppDbContext(_usageDbOptions) { CurrentUserId = profile.UserId };
        var logRow = await db.ClaudeUsageLogs
            .OrderByDescending(l => l.Id)
            .FirstAsync(cancellationToken);

        var usage = new EvaluationUsage(
            Provider: "anthropic",
            Model: Model,
            InputTokens: logRow.InputTokens,
            OutputTokens: logRow.OutputTokens,
            CacheReadInputTokens: logRow.CacheReadInputTokens,
            CacheCreationInputTokens: logRow.CacheCreationInputTokens);

        return new ProviderEvaluationResult(evaluation, usage, stopwatch.Elapsed);
    }
}
