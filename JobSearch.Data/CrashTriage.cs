namespace JobSearch.Data;

public record TriageDecision(bool ShouldDispatch, string Reason);

// Decides whether a newly-created Sentry issue is worth waking the automated fix agent for.
//
// This runs in C# rather than in the agent's prompt on purpose: every dispatch costs a full
// Claude Code session, so the obviously-not-actionable cases have to be filtered out for free.
// The agent should only ever see issues a human would also agree are worth investigating.
public static class CrashTriage
{
    // Only genuine failures. Sentry also emits warning/info-level issues, which are
    // diagnostics rather than crashes.
    private static readonly HashSet<string> ActionableLevels =
        new(StringComparer.OrdinalIgnoreCase) { "error", "fatal" };

    // Known non-actionable noise. Deliberately short and specific — this list is meant to grow
    // as real noise shows up, not to pre-guess it. Every entry here is something observed in
    // production that no code change on our side would fix.
    //
    // The job boards return 403 to datacenter IPs as a matter of policy; the fetch failure is
    // already handled (the pipeline falls back to cross-checking Jora/Adzuna, then to alert
    // metadata), so it is expected behaviour rather than a defect.
    private static readonly string[] IgnoredPatterns =
    [
        "Response status code does not indicate success: 403",
        "Response status code does not indicate success: 429",
    ];

    public static TriageDecision Evaluate(
        string? level,
        string? title,
        bool alreadyDispatched,
        int dispatchesInLastHour,
        int hourlyCap)
    {
        // Dedup first: a Sentry issue is created once, but webhook delivery retries and
        // manual replays both happen, and neither should re-run the agent.
        if (alreadyDispatched)
            return new(false, "already dispatched for this issue");

        if (!ActionableLevels.Contains(level ?? ""))
            return new(false, $"level '{level}' is not error or fatal");

        var haystack = title ?? "";
        var noiseMatch = IgnoredPatterns.FirstOrDefault(p => haystack.Contains(p, StringComparison.OrdinalIgnoreCase));
        if (noiseMatch is not null)
            return new(false, $"matches known-noise pattern '{noiseMatch}'");

        // The burst guard. Sentry's own per-issue alert throttling cannot help here: a bad
        // deploy produces many *distinct* new issues at once, each of which is a separate
        // first-seen event. Without a global cap that becomes N concurrent agent runs and N
        // times the token spend, so anything past the cap is dropped for a human to look at.
        if (dispatchesInLastHour >= hourlyCap)
            return new(false, $"hourly dispatch cap of {hourlyCap} already reached");

        return new(true, "actionable");
    }
}
