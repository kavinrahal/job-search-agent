using JobSearch.Data;
using PostingEvaluatorBenchmark.Fixtures;
using PostingEvaluatorBenchmark.Pricing;
using PostingEvaluatorBenchmark.Providers;
using PostingEvaluatorBenchmark.Scoring;

namespace PostingEvaluatorBenchmark;

public record ModelRunSummary(
    string ModelName,
    int TotalCases,
    int SucceededCases,
    decimal TotalCostUsd,
    decimal AvgCostPerEvalUsd,
    double AvgOutputTokens,
    double DisqualifierExactAccuracy,
    double DiscardBoundaryAccuracy,
    double DimensionAccuracy,
    IReadOnlyList<(string PairId, string Error)> Errors);

// Orchestrates the whole benchmark: for each requested model, runs every BenchmarkSuite case
// sequentially through ClaudeEvaluationProvider (see that class for why sequential, not
// concurrent -- matches the task brief's "sample-size/variety, not a throughput stress test"
// framing) and aggregates cost + accuracy. A single case throwing (a real API error, a malformed
// tool-use response ClaudeToolCallRetry couldn't recover) is recorded and skipped rather than
// aborting the whole run -- 209 other cases still produce a usable comparison even if one call
// failed.
public static class BenchmarkRunner
{
    public static async Task<IReadOnlyList<ModelRunSummary>> RunAsync(
        string apiKey,
        IReadOnlyList<string> models,
        IReadOnlyList<BenchmarkCase>? cases = null,
        TextWriter? log = null,
        CancellationToken cancellationToken = default)
    {
        cases ??= BenchmarkSuite.All;
        log ??= TextWriter.Null;

        var summaries = new List<ModelRunSummary>();
        foreach (var model in models)
        {
            log.WriteLine($"Running {cases.Count} cases against {model}...");
            summaries.Add(await RunModelAsync(apiKey, model, cases, log, cancellationToken));
        }
        return summaries;
    }

    private static async Task<ModelRunSummary> RunModelAsync(
        string apiKey, string model, IReadOnlyList<BenchmarkCase> cases, TextWriter log, CancellationToken cancellationToken)
    {
        var provider = new ClaudeEvaluationProvider(apiKey, model);

        decimal totalCost = 0m;
        long totalOutputTokens = 0;
        int succeeded = 0;
        int disqualifierExactHits = 0;
        int discardBoundaryHits = 0;
        int dimensionsGradedTotal = 0;
        int dimensionsCorrectTotal = 0;
        var errors = new List<(string, string)>();

        int i = 0;
        foreach (var benchmarkCase in cases)
        {
            i++;
            var profile = BuildSyntheticProfile(benchmarkCase.Criteria);

            try
            {
                var result = await provider.EvaluateAsync(profile, benchmarkCase.Posting.Render(), sourceUrl: null, cancellationToken);
                var score = AccuracyScorer.Score(benchmarkCase, result.Evaluation);

                succeeded++;
                totalCost += ModelPricing.CostUsd(result.Usage);
                totalOutputTokens += result.Usage.OutputTokens;
                if (score.DisqualifierExactMatch) disqualifierExactHits++;
                if (score.DiscardBooleanMatch) discardBoundaryHits++;
                dimensionsGradedTotal += score.DimensionsGraded;
                dimensionsCorrectTotal += score.DimensionsCorrect;
            }
            catch (Exception ex)
            {
                errors.Add((benchmarkCase.PairId, $"{ex.GetType().Name}: {ex.Message}"));
            }

            if (i % 25 == 0) log.WriteLine($"  [{model}] {i}/{cases.Count} cases run...");
        }

        return new ModelRunSummary(
            ModelName: model,
            TotalCases: cases.Count,
            SucceededCases: succeeded,
            TotalCostUsd: totalCost,
            AvgCostPerEvalUsd: succeeded == 0 ? 0 : totalCost / succeeded,
            AvgOutputTokens: succeeded == 0 ? 0 : (double)totalOutputTokens / succeeded,
            DisqualifierExactAccuracy: succeeded == 0 ? 0 : (double)disqualifierExactHits / succeeded,
            DiscardBoundaryAccuracy: succeeded == 0 ? 0 : (double)discardBoundaryHits / succeeded,
            DimensionAccuracy: dimensionsGradedTotal == 0 ? 0 : (double)dimensionsCorrectTotal / dimensionsGradedTotal,
            Errors: errors);
    }

    // Synthetic background/CV text -- PostingEvaluator's system prompt only actually reads
    // profile.JobCriteria (see BuildSystemPrompt), but UserProfile.Background/CvBase are non-
    // nullable strings, so these are just harmless placeholders, never sent to the model.
    private static UserProfile BuildSyntheticProfile(CriteriaProfile criteria) => new()
    {
        UserId = 1,
        Background = "Synthetic benchmark profile -- not sent to the evaluator.",
        CvBase = "Synthetic benchmark profile -- not sent to the evaluator.",
        JobCriteria = criteria.ToJobCriteriaYaml(),
    };
}
