using System.Net;
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

    // TC06 — SSRF guard: every private/loopback/link-local range this app must never let
    // postingUrl reach, including the cloud metadata address specifically.
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")] // cloud metadata endpoint
    [InlineData("0.0.0.0")]
    [InlineData("::1")]
    [InlineData("fe80::1")] // IPv6 link-local
    public void IsPubliclyRoutable_PrivateOrLoopbackOrLinkLocal_ReturnsFalse(string ip)
    {
        Assert.False(JobPostingFetcher.IsPubliclyRoutable(IPAddress.Parse(ip)));
    }

    // TC07 — Real public addresses (and the boundary just outside 172.16.0.0/12) must still
    // be allowed through — this app's entire job is fetching arbitrary public job postings.
    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("172.15.255.255")]
    [InlineData("172.32.0.0")]
    public void IsPubliclyRoutable_PublicAddress_ReturnsTrue(string ip)
    {
        Assert.True(JobPostingFetcher.IsPubliclyRoutable(IPAddress.Parse(ip)));
    }

    // TC08 — End-to-end SSRF guard, real network: a literal cloud-metadata-range IP must
    // never reach a socket connect, verified against the actual fetch path (not just the
    // pure IsPublicRoutable check) since that's what the ConnectCallback wiring itself needs
    // proving, not just the predicate it calls.
    [Fact]
    [Trait("Category", "contract")]
    public async Task FetchAsync_CloudMetadataAddress_Throws()
    {
        var fetcher = new JobPostingFetcher();

        await Assert.ThrowsAsync<HttpRequestException>(() => fetcher.FetchAsync("http://169.254.169.254/"));
    }

    // TC09 — Sanity check that the SSRF guard doesn't collaterally break real fetches.
    // Not example.com — its page is under the fetcher's 3000-char bot-challenge threshold,
    // an unrelated pre-existing quirk of LooksLikeBotChallenge, not something this test cares
    // about. Needs a real page large enough to clear that heuristic.
    [Fact]
    [Trait("Category", "contract")]
    public async Task FetchAsync_RealPublicUrl_Succeeds()
    {
        var fetcher = new JobPostingFetcher();

        var text = await fetcher.FetchAsync("https://en.wikipedia.org/wiki/Software_engineering");

        Assert.False(string.IsNullOrWhiteSpace(text));
    }
}
