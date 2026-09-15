using System.Globalization;
using System.Text;

namespace PostingEvaluatorBenchmark;

// Prints the comparison table the task brief asked for: per model, total cost, average cost per
// evaluation, average output tokens, and accuracy against ground truth broken out by disqualifier
// detection specifically (the safety-critical dimension -- see AccuracyScorer's doc comment).
public static class BenchmarkReport
{
    public static string Render(IReadOnlyList<ModelRunSummary> summaries)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("PostingEvaluator model comparison");
        sb.AppendLine("==================================");
        sb.AppendLine();

        sb.AppendLine(FormatRow("Model", "Cases", "Total $", "Avg $/eval", "Avg out tok", "Discard-boundary acc", "Disqualifier-exact acc", "Dimension acc"));
        sb.AppendLine(new string('-', 130));
        foreach (var s in summaries)
        {
            sb.AppendLine(FormatRow(
                s.ModelName,
                $"{s.SucceededCases}/{s.TotalCases}",
                "$" + s.TotalCostUsd.ToString("F4", CultureInfo.InvariantCulture),
                "$" + s.AvgCostPerEvalUsd.ToString("F6", CultureInfo.InvariantCulture),
                s.AvgOutputTokens.ToString("F0", CultureInfo.InvariantCulture),
                s.DiscardBoundaryAccuracy.ToString("P1", CultureInfo.InvariantCulture),
                s.DisqualifierExactAccuracy.ToString("P1", CultureInfo.InvariantCulture),
                s.DimensionAccuracy.ToString("P1", CultureInfo.InvariantCulture)));
        }
        sb.AppendLine();

        foreach (var s in summaries)
        {
            if (s.Errors.Count == 0) continue;
            sb.AppendLine($"{s.ModelName}: {s.Errors.Count} case(s) failed to complete:");
            foreach (var (pairId, error) in s.Errors.Take(10))
                sb.AppendLine($"    [{pairId}] {error}");
            if (s.Errors.Count > 10)
                sb.AppendLine($"    ... and {s.Errors.Count - 10} more.");
        }

        return sb.ToString();
    }

    private static string FormatRow(params string[] cols) =>
        string.Join("  ", cols.Select((c, i) => c.PadRight(i == 0 ? 18 : 14)));
}
