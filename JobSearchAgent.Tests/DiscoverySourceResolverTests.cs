using JobSearch.Data;
using JobSearchAgent.Integrations;
using JobSearchAgent.Workers;

namespace JobSearchAgent.Tests;

public class DiscoverySourceResolverTests
{
    // TC01 — Null selection (Tier 2, "choose your sources" not completed yet) defaults to
    // every automatic source, including Adzuna when creds are configured. This is the whole
    // point of the ticket: discovery has value with zero setup.
    [Fact]
    public void Resolve_NullSelection_WithAdzunaCreds_ReturnsAllAutomaticSources()
    {
        var result = DiscoverySourceResolver.Resolve(null, "app-id", "app-key");

        Assert.Equal(3, result.Count);
        Assert.Contains(result, f => f is AdzunaFetcher);
        Assert.Contains(result, f => f is GreenhouseFetcher);
        Assert.Contains(result, f => f is LeverFetcher);
    }

    // TC02 — Null selection but no Adzuna creds configured (env not set): Adzuna is silently
    // excluded rather than the whole discovery run being skipped, as it was before this
    // ticket — Greenhouse/Lever need no creds and shouldn't be held hostage by Adzuna's.
    [Fact]
    public void Resolve_NullSelection_NoAdzunaCreds_ExcludesAdzunaOnly()
    {
        var result = DiscoverySourceResolver.Resolve(null, null, null);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, f => f is AdzunaFetcher);
    }

    // TC03 — Explicit empty selection (user picked zero sources and saved) is respected as
    // "none", not treated the same as "hasn't chosen yet". Silent failure risk: collapsing
    // these two states would either ignore an explicit opt-out or never show new users anything.
    [Fact]
    public void Resolve_ExplicitEmptySelection_ReturnsNone()
    {
        var result = DiscoverySourceResolver.Resolve("", "app-id", "app-key");

        Assert.Empty(result);
    }

    // TC04 — Explicit partial selection only returns the chosen sources, even when Adzuna
    // creds are available — an unselected automatic source must not run anyway.
    [Fact]
    public void Resolve_ExplicitPartialSelection_OnlyReturnsChosenSources()
    {
        var result = DiscoverySourceResolver.Resolve("lever", "app-id", "app-key");

        Assert.Single(result);
        Assert.IsType<LeverFetcher>(result[0]);
    }

    // TC05 — An unrecognized key in the stored CSV (future catalog change, or bad data)
    // doesn't throw and doesn't produce a fetcher for it.
    [Fact]
    public void Resolve_UnknownKeyInSelection_IgnoredWithoutThrowing()
    {
        var result = DiscoverySourceResolver.Resolve("lever,not_a_real_source", "app-id", "app-key");

        Assert.Single(result);
        Assert.IsType<LeverFetcher>(result[0]);
    }
}
