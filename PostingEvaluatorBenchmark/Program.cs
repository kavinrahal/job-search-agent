using PostingEvaluatorBenchmark;
using PostingEvaluatorBenchmark.Fixtures;

// PostingEvaluator model comparison benchmark.
//
// WHAT THIS IS: runs PostingEvaluator's real production call path (via its narrow, additive,
// test-only `model` constructor override -- see JobSearch.Data/PostingEvaluator.cs) against
// ~205 synthetic, ground-truth-labeled (criteria, posting) pairs (see Fixtures/BenchmarkSuite.cs
// for the sampling strategy) for each of claude-sonnet-5 (current production default),
// claude-opus-4-8, and claude-haiku-4-5, then prints a cost/accuracy comparison table.
//
// WHAT THIS DOES NOT DO: call OpenAI or Gemini (no API keys are set up for either yet -- see
// Providers/IEvaluationProvider.cs for the documented extension point once they are), or run
// live by default (see the ANTHROPIC_API_KEY check below).
//
// HOW TO RUN:
//   ANTHROPIC_API_KEY=sk-... dotnet run --project PostingEvaluatorBenchmark
//
// Optional env vars:
//   BENCHMARK_MODELS   comma-separated model ids to run (default: all three production tiers).
//                       e.g. BENCHMARK_MODELS=claude-haiku-4-5 for a cheap smoke test first.
//   BENCHMARK_SAMPLE   run only the first N cases (default: all ~205) -- useful for a quick,
//                       low-cost trial run before committing to the full suite across 3 models.
//
// Without ANTHROPIC_API_KEY set, this prints a message and exits cleanly (exit code 0) -- no
// API calls are made. This mirrors JobSearchAgent.Tests' contract-test gating convention (see
// CvTailorAgentModelEvalTests, EmailClassifierEvalTests) so this tool is always safe to build,
// commit, and CI-check without ever making a real, billed call.

string? apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("ANTHROPIC_API_KEY is not set -- nothing to do. See this file's header comment for how to run a live benchmark.");
    return 0;
}

var models = (Environment.GetEnvironmentVariable("BENCHMARK_MODELS") ?? "claude-sonnet-5,claude-opus-4-8,claude-haiku-4-5")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

var cases = BenchmarkSuite.All.AsEnumerable();
if (int.TryParse(Environment.GetEnvironmentVariable("BENCHMARK_SAMPLE"), out int sampleSize) && sampleSize > 0)
    cases = cases.Take(sampleSize);

var summaries = await BenchmarkRunner.RunAsync(apiKey, models, cases.ToList(), Console.Out);

Console.WriteLine(BenchmarkReport.Render(summaries));
return 0;
