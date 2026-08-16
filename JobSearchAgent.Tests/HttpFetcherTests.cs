using System.Net;
using JobSearch.Data;
using JobSearchAgent.Integrations;

namespace JobSearchAgent.Tests;

public class HttpFetcherTests
{
    // -------------------------------------------------------------------------
    // Shared stub handler
    // -------------------------------------------------------------------------

    private static HttpClient Stub(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(new StubHandler(json, status));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", name));

    private sealed class StubHandler(string json, HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage _, CancellationToken __) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            });
    }

    // =========================================================================
    // GreenhouseFetcher
    // =========================================================================

    // TC01 — AU job in response → mapped to JobFeedItem with correct fields
    [Fact]
    public async Task Greenhouse_AuJob_MappedToFeedItem()
    {
        var fetcher = new GreenhouseFetcher(Stub(Fixture("greenhouse_response.json")));

        var items = await fetcher.FetchAllAsync();

        Assert.Contains(items, i => i.Title == "Software Engineer" && i.Company == "Canva");
    }

    // TC02 — London job filtered out; null-location job (remote) included
    // Silent failure: overseas jobs reach the evaluator, consuming Claude quota on irrelevant postings.
    [Fact]
    public async Task Greenhouse_NonAuJobExcluded_NullLocationIncluded()
    {
        var fetcher = new GreenhouseFetcher(Stub(Fixture("greenhouse_response.json")));

        var items = await fetcher.FetchAllAsync();

        Assert.DoesNotContain(items, i => i.Title == "Backend Engineer"); // London
        Assert.Contains(items, i => i.Title == "Platform Engineer");      // null location → remote
    }

    // TC03 — HTML stripped from Content field before it lands in Description
    [Fact]
    public async Task Greenhouse_HtmlContent_StrippedInDescription()
    {
        var fetcher = new GreenhouseFetcher(Stub(Fixture("greenhouse_response.json")));

        var items = await fetcher.FetchAllAsync();

        var swEng = items.Single(i => i.Title == "Software Engineer");
        Assert.DoesNotContain("<p>", swEng.Description);
        Assert.Contains("Requirements", swEng.Description);
    }

    // TC04 — 404 from a company endpoint: caught and skipped, no exception propagated
    // Silent failure: unhandled HttpRequestException terminates the loop, skipping all subsequent companies.
    [Fact]
    public async Task Greenhouse_404Response_SkippedWithoutThrowing()
    {
        var fetcher = new GreenhouseFetcher(Stub("{}", HttpStatusCode.NotFound));

        var ex = await Record.ExceptionAsync(() => fetcher.FetchAllAsync());

        Assert.Null(ex);
    }

    // =========================================================================
    // LeverFetcher
    // =========================================================================

    // TC05 — AU job (Sydney, NSW, Australia) included; London job excluded; null location included
    [Fact]
    public async Task Lever_AuJobIncluded_NonAuExcluded_NullIncluded()
    {
        var fetcher = new LeverFetcher(Stub(Fixture("lever_response.json")));

        var items = await fetcher.FetchAllAsync();

        Assert.Contains(items, i => i.Title == "Senior Software Engineer");  // Sydney AU
        Assert.DoesNotContain(items, i => i.Title == "Staff Engineer");      // London
        Assert.Contains(items, i => i.Title == "Remote SRE");                // null location
    }

    // TC06 — descriptionPlain preferred over HTML description when non-empty
    [Fact]
    public async Task Lever_DescriptionPlainPreferred_OverHtmlDescription()
    {
        var fetcher = new LeverFetcher(Stub(Fixture("lever_response.json")));

        var items = await fetcher.FetchAllAsync();

        var seniorEng = items.Single(i => i.Title == "Senior Software Engineer");
        // descriptionPlain = "Work on core platform." — no HTML tags
        Assert.DoesNotContain("<p>", seniorEng.Description);
        Assert.Contains("Work on core platform", seniorEng.Description);
    }

    // TC07 — 404 caught, no exception propagated
    [Fact]
    public async Task Lever_404Response_SkippedWithoutThrowing()
    {
        var fetcher = new LeverFetcher(Stub("[]", HttpStatusCode.NotFound));

        var ex = await Record.ExceptionAsync(() => fetcher.FetchAllAsync());

        Assert.Null(ex);
    }

    // =========================================================================
    // AdzunaFetcher
    // =========================================================================

    // TC08 — Valid job with salary mapped to JobFeedItem with correct salary fields
    [Fact]
    public async Task Adzuna_ValidJob_MappedWithSalary()
    {
        var fetcher = new AdzunaFetcher("id", "key", Stub(Fixture("adzuna_response.json")));

        var items = await fetcher.FetchAllAsync();

        var job = items.Single(i => i.Title == "Full Stack Developer");
        Assert.Equal(110_000, job.SalaryMin);
        Assert.Equal(140_000, job.SalaryMax);
        Assert.Equal("Acme Corp", job.Company);
        Assert.Equal("https://www.adzuna.com.au/jobs/details/9001", job.Url);
    }

    // TC09 — Job with empty redirect_url filtered out
    // Silent failure: empty URL stored in DB would break every downstream operation.
    [Fact]
    public async Task Adzuna_EmptyRedirectUrl_Filtered()
    {
        var fetcher = new AdzunaFetcher("id", "key", Stub(Fixture("adzuna_response.json")));

        var items = await fetcher.FetchAllAsync();

        Assert.DoesNotContain(items, i => i.Company == "Empty URL Co");
    }

    // TC10 — Non-200 response for one keyword: caught, returns items from other keywords
    [Fact]
    public async Task Adzuna_OneKeyword404_OtherKeywordsContinue()
    {
        // First call fails, all subsequent succeed
        int callCount = 0;
        var handler = new CountingStubHandler(() =>
        {
            callCount++;
            return callCount == 1
                ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                : new HttpResponseMessage(HttpStatusCode.OK)
                  { Content = new StringContent(Fixture("adzuna_response.json"),
                        System.Text.Encoding.UTF8, "application/json") };
        });
        var fetcher = new AdzunaFetcher("id", "key", new HttpClient(handler));

        var items = await fetcher.FetchAllAsync();

        // Should have results from the 4 successful keywords (5 total, 1 failed)
        Assert.NotEmpty(items);
    }

    private sealed class CountingStubHandler(Func<HttpResponseMessage> factory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage _, CancellationToken __) =>
            Task.FromResult(factory());
    }

    // -------------------------------------------------------------------------
    // AdzunaFetcher — company-driven pagination (same shape as Jora's, see below —
    // a real structured API here rather than a scrape, so paging further is low-risk)
    // -------------------------------------------------------------------------

    private const string AdzunaPageTwoWithCodafication = """
        {
          "results": [
            {
              "title": "Software Engineer",
              "description": "Build things.",
              "redirect_url": "https://www.adzuna.com.au/jobs/details/9999",
              "created": "2026-06-11T00:00:00Z",
              "location": { "display_name": "Melbourne, VIC" },
              "company": { "display_name": "Codafication" },
              "salary_min": null,
              "salary_max": null,
              "contract_type": null,
              "contract_time": null
            }
          ],
          "count": 1
        }
        """;

    // TC19 — no company given → exactly one request (matches the pre-pagination behavior;
    // JobAlertProcessor's cross-check has no separate company field and must not silently
    // start paying for 5x the requests per failed alert).
    [Fact]
    public async Task Adzuna_SearchWithoutCompany_FetchesOnlyOnePage()
    {
        var handler = new SequenceStubHandler(Fixture("adzuna_response.json"), AdzunaPageTwoWithCodafication);
        var fetcher = new AdzunaFetcher("id", "key", new HttpClient(handler));

        await fetcher.SearchAsync("software engineer", "melbourne");

        Assert.Single(handler.RequestedUris);
    }

    // TC20 — company given, not on page 1, found on page 2 → pages until found, then stops.
    [Fact]
    public async Task Adzuna_SearchWithCompany_PagesUntilFound_ThenStops()
    {
        var handler = new SequenceStubHandler(Fixture("adzuna_response.json"), AdzunaPageTwoWithCodafication);
        var fetcher = new AdzunaFetcher("id", "key", new HttpClient(handler));

        var items = await fetcher.SearchAsync("software engineer", "melbourne", "Codafication");

        Assert.Equal(2, handler.RequestedUris.Count);
        Assert.Contains(items, i => i.Company == "Codafication");
    }

    // TC21 — company given and found immediately on page 1 → no wasted extra requests.
    [Fact]
    public async Task Adzuna_SearchWithCompany_FoundOnFirstPage_MakesOneRequest()
    {
        var handler = new SequenceStubHandler(Fixture("adzuna_response.json"));
        var fetcher = new AdzunaFetcher("id", "key", new HttpClient(handler));

        await fetcher.SearchAsync("full stack developer", "melbourne", "Acme Corp");

        Assert.Single(handler.RequestedUris);
    }

    // TC22 — company given but never found across every page → bounded at 5 requests.
    [Fact]
    public async Task Adzuna_SearchWithCompany_NeverFound_StopsAtPageCap()
    {
        var handler = new SequenceStubHandler(Fixture("adzuna_response.json"));
        var fetcher = new AdzunaFetcher("id", "key", new HttpClient(handler));

        await fetcher.SearchAsync("software engineer", "melbourne", "Nonexistent Company");

        Assert.Equal(5, handler.RequestedUris.Count);
    }

    // =========================================================================
    // JoraFetcher
    // =========================================================================

    // TC11 — job cards parsed into JobFeedItems with title/company/location/url from the
    // save-button's data-* attributes, paired with the matching href for the URL.
    [Fact]
    public async Task Jora_SearchResults_MappedToFeedItems()
    {
        var fetcher = new JoraFetcher(Stub(Fixture("jora_search.html")));

        var items = await fetcher.SearchAsync("software engineer", "Melbourne");

        var job = items.Single(i => i.Company == "Mintec Systems");
        Assert.Equal("Software Engineer - Java Developer", job.Title);
        Assert.Equal("Melbourne VIC", job.Location);
        Assert.Equal("https://au.jora.com/job/Software-Engineer-7fd5d2fc90da82fe6c17abf9ada95262", job.Url);
        Assert.Equal("jora", job.Source);
    }

    // TC12 — both cards in the fixture are parsed, not just the first
    [Fact]
    public async Task Jora_SearchResults_AllCardsParsed()
    {
        var fetcher = new JoraFetcher(Stub(Fixture("jora_search.html")));

        var items = await fetcher.SearchAsync("cabinet maker", "Melbourne");

        Assert.Equal(2, items.Count);
    }

    // TC13 — request failure returns an empty list rather than throwing
    // Silent failure: an unhandled exception here would abort the whole cross-check attempt.
    [Fact]
    public async Task Jora_RequestFails_ReturnsEmptyList()
    {
        var fetcher = new JoraFetcher(Stub("", HttpStatusCode.ServiceUnavailable));

        var items = await fetcher.SearchAsync("software engineer", "Melbourne");

        Assert.Empty(items);
    }

    // TC14 — requests the "pretty URL" pattern (/{keywords}-jobs-in-{location}), not the
    // query-string form (/j?q=...&l=...) — confirmed live that Jora blocks the query-string
    // endpoint (403) from this app's host while leaving the pretty-URL form open. Silent
    // failure: reverting to ?q= here would silently zero out every Jora result again, exactly
    // what happened before this was caught.
    [Fact]
    public async Task Jora_Search_RequestsPrettyUrlNotQueryString()
    {
        var handler = new RecordingStubHandler(Fixture("jora_search.html"));
        var fetcher = new JoraFetcher(new HttpClient(handler));

        await fetcher.SearchAsync("full stack .net developer", "Melbourne");

        Assert.NotNull(handler.RequestedUri);
        Assert.DoesNotContain("?q=", handler.RequestedUri!.ToString());
        Assert.Contains("/full-stack-net-developer-jobs-in-Melbourne", handler.RequestedUri.ToString());
    }

    private sealed class RecordingStubHandler(string html) : HttpMessageHandler
    {
        public Uri? RequestedUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken _)
        {
            RequestedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html"),
            });
        }
    }

    // -------------------------------------------------------------------------
    // JoraFetcher — company-driven pagination
    // -------------------------------------------------------------------------

    // A second page's worth of results, containing a company neither present nor a partial
    // match for anything in jora_search.html's two cards (Lead Group Consultancy, Mintec
    // Systems) — stands in for "the real listing is buried past page 1".
    private const string JoraPageTwoWithCodafication = """
        <!DOCTYPE html><html><body>
        <div class="job-card">
          <button name="button" type="submit" class="tertiary save-job-button" data-job-id="cdf001" data-tk="tok" data-saved="false" data-disabled="" data-ga4="{}" data-job-title="Software Engineer" data-location="Melbourne VIC" data-company-name="Codafication">Save</button>
          <a href="/job/Software-Engineer-cdf001">Software Engineer</a>
        </div>
        </body></html>
        """;

    private sealed class SequenceStubHandler(params string[] pages) : HttpMessageHandler
    {
        public List<Uri> RequestedUris { get; } = [];
        private int _index;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken _)
        {
            RequestedUris.Add(request.RequestUri!);
            var body = pages[Math.Min(_index, pages.Length - 1)];
            _index++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "text/html"),
            });
        }
    }

    // TC15 — no company given → exactly one request, even though a second page (with more
    // results) is available. Matters because JobAlertProcessor's cross-check has no separate
    // company field and must not silently start paying for 5x the requests per failed alert.
    [Fact]
    public async Task Jora_SearchWithoutCompany_FetchesOnlyOnePage()
    {
        var handler = new SequenceStubHandler(Fixture("jora_search.html"), JoraPageTwoWithCodafication);
        var fetcher = new JoraFetcher(new HttpClient(handler));

        await fetcher.SearchAsync("software engineer", "Melbourne");

        Assert.Single(handler.RequestedUris);
    }

    // TC16 — company given, not on page 1, found on page 2 → pages fetched until it's found,
    // then stops (doesn't burn through all 5 allowed pages once the target is in hand).
    [Fact]
    public async Task Jora_SearchWithCompany_PagesUntilFound_ThenStops()
    {
        var handler = new SequenceStubHandler(Fixture("jora_search.html"), JoraPageTwoWithCodafication);
        var fetcher = new JoraFetcher(new HttpClient(handler));

        var items = await fetcher.SearchAsync("software engineer", "Melbourne", "Codafication");

        Assert.Equal(2, handler.RequestedUris.Count);
        Assert.Contains(items, i => i.Company == "Codafication");
    }

    // TC17 — company given and found immediately on page 1 → no wasted extra requests.
    [Fact]
    public async Task Jora_SearchWithCompany_FoundOnFirstPage_MakesOneRequest()
    {
        var handler = new SequenceStubHandler(Fixture("jora_search.html"));
        var fetcher = new JoraFetcher(new HttpClient(handler));

        await fetcher.SearchAsync("software engineer", "Melbourne", "Mintec Systems");

        Assert.Single(handler.RequestedUris);
    }

    // TC18 — company given but never found across every page → bounded at 5 requests, not
    // an unbounded/runaway loop.
    [Fact]
    public async Task Jora_SearchWithCompany_NeverFound_StopsAtPageCap()
    {
        var handler = new SequenceStubHandler(Fixture("jora_search.html"));
        var fetcher = new JoraFetcher(new HttpClient(handler));

        await fetcher.SearchAsync("software engineer", "Melbourne", "Nonexistent Company");

        Assert.Equal(5, handler.RequestedUris.Count);
    }
}
