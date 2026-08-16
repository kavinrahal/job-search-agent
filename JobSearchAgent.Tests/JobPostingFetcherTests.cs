using JobSearch.Data;

namespace JobSearchAgent.Tests;

public class JobPostingFetcherTests
{
    // TC01 — Tracking query params stripped from a known job-board host, canonical path kept.
    // This is the exact production failure: a SERP-copied Jora link with a session-bound
    // token gets 403'd by Cloudflare even though the bare job URL fetches fine.
    [Fact]
    public void StripTrackingParams_JoraUrlWithTrackingParams_StripsQueryKeepsPath()
    {
        var result = JobPostingFetcher.StripTrackingParams(
            "https://au.jora.com/job/Software-Engineer-d6556f5f86e51a1f5ac0f5596d7750f1" +
            "?abstract_type=extended_llm&sol_key=fb2732ec7f39170736bef2ce87dcc387&tk=QdUWRTCQpoxbM44y9mz3");

        Assert.Equal("https://au.jora.com/job/Software-Engineer-d6556f5f86e51a1f5ac0f5596d7750f1", result);
    }

    // TC02 — Same behavior on Seek and LinkedIn, the other two hosts with tracking-heavy
    // SERP links (see JobAlertProcessor's canonical URL construction for these hosts).
    [Theory]
    [InlineData("https://au.seek.com/job/12345?ref=serp&pos=1", "https://au.seek.com/job/12345")]
    [InlineData("https://www.linkedin.com/jobs/view/98765?trackingId=abc", "https://www.linkedin.com/jobs/view/98765")]
    public void StripTrackingParams_OtherKnownHosts_StripsQuery(string input, string expected)
    {
        Assert.Equal(expected, JobPostingFetcher.StripTrackingParams(input));
    }

    // TC03 — Already-bare URL on a known host is returned unchanged (idempotent — this
    // matters since DiagnoseAsync always calls it, including for links our own candidate
    // picker already generates without a query string).
    [Fact]
    public void StripTrackingParams_AlreadyBareKnownHostUrl_Unchanged()
    {
        var url = "https://au.jora.com/job/some-slug";

        Assert.Equal(url, JobPostingFetcher.StripTrackingParams(url));
    }

    // TC04 — Unknown host's query string is left alone. Silent failure risk: stripping
    // indiscriminately would break a company careers page whose query params are load-bearing
    // (e.g. a Greenhouse board's own paging/embed params).
    [Fact]
    public void StripTrackingParams_UnknownHost_QueryStringPreserved()
    {
        var url = "https://boards.greenhouse.io/acme/jobs/12345?gh_src=abc123";

        Assert.Equal(url, JobPostingFetcher.StripTrackingParams(url));
    }

    // TC05 — Malformed input doesn't throw; returned as-is for the caller's own error
    // handling further down the fetch pipeline to report.
    [Fact]
    public void StripTrackingParams_MalformedUrl_ReturnsInputUnchanged()
    {
        var url = "not a url";

        Assert.Equal(url, JobPostingFetcher.StripTrackingParams(url));
    }
}
