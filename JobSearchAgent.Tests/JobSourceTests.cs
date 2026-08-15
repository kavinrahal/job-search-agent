using JobSearch.Data;

namespace JobSearchAgent.Tests;

public class JobSourceTests
{
    // TC01 — Unknown keys dropped. This is the sole defense the PUT /sources endpoint relies
    // on against a client posting arbitrary strings into User.EnabledSources.
    [Fact]
    public void Sanitize_UnknownKeyMixedWithKnown_DropsUnknown()
    {
        var result = JobSource.Sanitize([JobSource.Adzuna, "not_a_real_source"]);

        Assert.Equal([JobSource.Adzuna], result);
    }

    // TC02 — Duplicate keys collapsed to one.
    [Fact]
    public void Sanitize_DuplicateKeys_ReturnsDistinct()
    {
        var result = JobSource.Sanitize([JobSource.Lever, JobSource.Lever]);

        Assert.Equal([JobSource.Lever], result);
    }

    // TC03 — Empty input, no exception, empty output.
    [Fact]
    public void Sanitize_EmptyInput_ReturnsEmpty()
    {
        var result = JobSource.Sanitize([]);

        Assert.Empty(result);
    }

    // TC04 — Regression guard: a future copy-paste when adding a source can't silently
    // duplicate an existing key (ValidKeys is built from Catalog as a HashSet, which would
    // swallow the duplicate without any error).
    [Fact]
    public void Catalog_AllKeysUnique()
    {
        var keys = JobSource.Catalog.Select(c => c.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    // TC05 — The automatic/alert-based split is a real behavioral contract (JobDiscoveryWorker
    // filters on it), not just display data — a flipped bool here silently misroutes a source.
    [Fact]
    public void Catalog_KnownSources_HaveExpectedAutomaticFlag()
    {
        bool AutomaticFlag(string key) => JobSource.Catalog.Single(c => c.Key == key).Automatic;

        Assert.True(AutomaticFlag(JobSource.Adzuna));
        Assert.True(AutomaticFlag(JobSource.Jooble));
        Assert.True(AutomaticFlag(JobSource.Greenhouse));
        Assert.True(AutomaticFlag(JobSource.Lever));
        Assert.False(AutomaticFlag(JobSource.SeekAlert));
        Assert.False(AutomaticFlag(JobSource.LinkedinAlert));
        Assert.False(AutomaticFlag(JobSource.IndeedAlert));
        Assert.False(AutomaticFlag(JobSource.JoraAlert));
    }
}
