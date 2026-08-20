using JobSearch.Data;
using JobSearchAgent.Integrations;

namespace JobSearchAgent.Workers;

// Turns a user's EnabledSources selection (see JobSource.cs) into the fetchers
// JobDiscoveryWorker should run against. Extracted from Program.cs so the branching is
// unit-testable — top-level-statement local functions can't be reached from a test project.
public static class DiscoverySourceResolver
{
    // Null EnabledSources (Tier 2, sources step not yet completed) defaults to every automatic
    // source, so discovery has value from the moment someone subscribes. An explicit empty
    // selection means the user turned everything off; that's respected as-is. Jooble has no
    // fetcher yet — selecting it is a no-op until one exists.
    //
    // adzunaKeywords is per-user (derived from their own job criteria via SearchKeywordAgent,
    // see JobSearchAgent/Program.cs) — not defaulted here to anything hardcoded. A missing or
    // empty list means Adzuna is skipped even if selected+configured, since a keyword-search
    // source with no keywords has nothing useful to do; that's the safe failure, not falling
    // back to some generic default that would only really suit one profession.
    public static List<IJobFetcher> Resolve(
        string? enabledSources, string? adzunaAppId, string? adzunaAppKey, IReadOnlyList<string>? adzunaKeywords = null)
    {
        var keys = enabledSources is null
            ? JobSource.Catalog.Where(c => c.Automatic).Select(c => c.Key).ToHashSet()
            : JobSource.Sanitize(enabledSources.Split(',', StringSplitOptions.RemoveEmptyEntries)).ToHashSet();

        var fetchers = new List<IJobFetcher>();
        if (keys.Contains(JobSource.Adzuna) && adzunaAppId is not null && adzunaAppKey is not null
            && adzunaKeywords is { Count: > 0 })
            fetchers.Add(new AdzunaFetcher(adzunaAppId, adzunaAppKey, adzunaKeywords));
        if (keys.Contains(JobSource.Greenhouse))
            fetchers.Add(new GreenhouseFetcher());
        if (keys.Contains(JobSource.Lever))
            fetchers.Add(new LeverFetcher());
        return fetchers;
    }
}
