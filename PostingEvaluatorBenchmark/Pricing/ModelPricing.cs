using PostingEvaluatorBenchmark.Providers;

namespace PostingEvaluatorBenchmark.Pricing;

// Current per-model pricing, USD per million tokens (MTok), per the task brief. Cache-read and
// cache-write multipliers are Anthropic's standard 5-minute-TTL prompt-caching rates, applied to
// the same model's base input price -- not separate per-model figures, since Anthropic prices
// cache read/write as a multiplier of that model's own input rate.
public record ModelRate(decimal InputPerMTok, decimal OutputPerMTok)
{
    public const decimal CacheReadMultiplier = 0.1m;
    public const decimal CacheWriteMultiplier = 1.25m;
}

public static class ModelPricing
{
    private static readonly Dictionary<string, ModelRate> Rates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["claude-sonnet-5"] = new ModelRate(InputPerMTok: 3m, OutputPerMTok: 15m),
        ["claude-opus-4-8"] = new ModelRate(InputPerMTok: 5m, OutputPerMTok: 25m),
        ["claude-haiku-4-5"] = new ModelRate(InputPerMTok: 1m, OutputPerMTok: 5m),
    };

    // Anthropic's Usage.InputTokens reports only freshly-processed input (excludes cache
    // read/creation tokens, which are billed separately below) -- see ClaudeUsageLogger/
    // ClaudeUsageLog, whose four token columns this mirrors exactly.
    public static decimal CostUsd(EvaluationUsage usage)
    {
        var rate = Rates.TryGetValue(usage.Model, out var r)
            ? r
            : throw new ArgumentException($"No pricing registered for model \"{usage.Model}\" -- add it to ModelPricing.Rates.");

        const decimal perMillion = 1_000_000m;
        decimal inputCost = usage.InputTokens * rate.InputPerMTok / perMillion;
        decimal cacheReadCost = usage.CacheReadInputTokens * rate.InputPerMTok * ModelRate.CacheReadMultiplier / perMillion;
        decimal cacheWriteCost = usage.CacheCreationInputTokens * rate.InputPerMTok * ModelRate.CacheWriteMultiplier / perMillion;
        decimal outputCost = usage.OutputTokens * rate.OutputPerMTok / perMillion;

        return inputCost + cacheReadCost + cacheWriteCost + outputCost;
    }

    public static bool IsKnownModel(string model) => Rates.ContainsKey(model);
}
