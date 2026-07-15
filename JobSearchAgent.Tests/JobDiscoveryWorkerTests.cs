using JobSearch.Data;
using JobSearchAgent.Integrations;
using JobSearchAgent.Workers;

namespace JobSearchAgent.Tests;

public class JobDiscoveryWorkerTests
{
    // =========================================================================
    // FormatPostingText
    // =========================================================================

    // TC01 — Both salary bounds present → "$X – $Y AUD" format
    [Fact]
    public void FormatPostingText_BothSalaryBounds_FormattedRange()
    {
        var item = FeedItem(salaryMin: 100_000, salaryMax: 140_000);

        var result = JobDiscoveryWorker.FormatPostingText(item);

        Assert.Contains("$100,000", result);
        Assert.Contains("$140,000", result);
        Assert.Contains("AUD", result);
    }

    // TC02 — Only min salary → "From $X AUD"
    [Fact]
    public void FormatPostingText_OnlyMinSalary_FromFormat()
    {
        var item = FeedItem(salaryMin: 90_000, salaryMax: null);

        var result = JobDiscoveryWorker.FormatPostingText(item);

        Assert.Contains("From $90,000 AUD", result);
    }

    // TC03 — No salary fields → "Not stated"
    // Silent failure: missing null-guard would produce empty salary line, confusing the evaluator.
    [Fact]
    public void FormatPostingText_NoSalary_NotStated()
    {
        var item = FeedItem(salaryMin: null, salaryMax: null);

        var result = JobDiscoveryWorker.FormatPostingText(item);

        Assert.Contains("Not stated", result);
    }

    // =========================================================================
    // RunAsync
    // =========================================================================

    // TC04 — All feed items older than 14 days → nothing new, (0, 0, 0)
    [Fact]
    public async Task RunAsync_AllItemsTooOld_ReturnsZeroTuple()
    {
        var db = Db.Fresh();
        var oldItem = FeedItem(publishedAt: DateTime.UtcNow.AddDays(-15));
        var worker = MakeWorker(db, fetchers: [new FakeFetcher([oldItem])]);

        var (discovered, evaluated, notified, _) = await worker.RunAsync();

        Assert.Equal(0, discovered);
        Assert.Equal(0, evaluated);
        Assert.Equal(0, notified);
    }

    // TC05 — Item already in DB with non-error rec → skipped, (0, 0, 0)
    [Fact]
    public async Task RunAsync_ItemAlreadyEvaluated_Skipped()
    {
        var db = Db.Fresh();
        var item = FeedItem();
        db.DiscoveredPostings.Add(new DiscoveredPosting
        {
            Url = item.Url, Source = "greenhouse", Title = item.Title,
            Recommendation = "good_match", DiscoveredAt = DateTime.UtcNow, EvaluatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        int evalCount = 0;
        var worker = MakeWorker(db,
            fetchers: [new FakeFetcher([item])],
            evaluator: new FakeEval(_ => { evalCount++; return StubEval("weak_match"); }));

        await worker.RunAsync();

        Assert.Equal(0, evalCount);
    }

    // TC06 — Description ≥ 400 chars → FormatPostingText used (no page fetch attempted)
    [Fact]
    public async Task RunAsync_LongDescription_UsesFeedDescription()
    {
        var db = Db.Fresh();
        int fetchCount = 0;
        var desc = new string('x', 400);
        var item = FeedItem(description: desc);
        string? capturedText = null;
        var worker = MakeWorker(db,
            fetchers: [new FakeFetcher([item])],
            pageFetcher: new FakePageFetcher(_ => { fetchCount++; return "full page"; }),
            evaluator: new FakeEval(text => { capturedText = text; return StubEval("weak_match"); }));

        await worker.RunAsync();

        Assert.Equal(0, fetchCount);
        Assert.NotNull(capturedText);
        Assert.Contains("x", capturedText);
    }

    // TC07 — Description < 400 chars, page fetch succeeds → uses full page text
    // Silent failure: the threshold branch not working means short-description postings
    // always evaluate on thin content, degrading match accuracy.
    [Fact]
    public async Task RunAsync_ShortDescriptionFetchSucceeds_UsesFullPageText()
    {
        var db = Db.Fresh();
        var item = FeedItem(description: "Short.");
        string? capturedText = null;
        var worker = MakeWorker(db,
            fetchers: [new FakeFetcher([item])],
            pageFetcher: new FakePageFetcher(_ => "full page content from web"),
            evaluator: new FakeEval(text => { capturedText = text; return StubEval("weak_match"); }));

        await worker.RunAsync();

        Assert.Equal("full page content from web", capturedText);
    }

    // TC08 — Description < 400 chars, page fetch throws → falls back to feed description
    [Fact]
    public async Task RunAsync_ShortDescriptionFetchFails_FallsBackToFeedText()
    {
        var db = Db.Fresh();
        var item = FeedItem(description: "Short.");
        string? capturedText = null;
        var worker = MakeWorker(db,
            fetchers: [new FakeFetcher([item])],
            pageFetcher: new FakePageFetcher(_ => throw new HttpRequestException("timeout")),
            evaluator: new FakeEval(text => { capturedText = text; return StubEval("weak_match"); }));

        await worker.RunAsync();

        Assert.NotNull(capturedText);
        Assert.Contains(item.Company, capturedText);
    }

    // TC09 — Evaluator throws → Recommendation="error", loop continues
    [Fact]
    public async Task RunAsync_EvaluatorThrows_RecommendationSetToError()
    {
        var db = Db.Fresh();
        var item = FeedItem();
        var worker = MakeWorker(db,
            fetchers: [new FakeFetcher([item])],
            evaluator: new FakeEval(_ => throw new InvalidOperationException("LLM down")));

        await worker.RunAsync();

        var record = db.DiscoveredPostings.Single(d => d.Url == item.Url);
        Assert.Equal("error", record.Recommendation);
    }

    // TC10 — strong_match + telegram → NotificationSent=true, notified=1
    [Fact]
    public async Task RunAsync_StrongMatchWithTelegram_NotificationSent()
    {
        var db = Db.Fresh();
        var telegram = new FakeTelegram(returns: true);
        var worker = MakeWorker(db,
            fetchers: [new FakeFetcher([FeedItem()])],
            evaluator: new FakeEval(_ => StubEval("strong_match")),
            telegram: telegram);

        var (_, _, notified, _) = await worker.RunAsync();

        var record = db.DiscoveredPostings.Single();
        Assert.True(record.NotificationSent);
        Assert.Equal(1, notified);
        Assert.Equal(1, telegram.CallCount);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static JobFeedItem FeedItem(
        string url = "https://boards.greenhouse.io/test/jobs/1",
        string description = "Standard description for testing purposes.",
        double? salaryMin = null,
        double? salaryMax = null,
        DateTime? publishedAt = null) => new()
    {
        Title       = "Software Engineer",
        Company     = "Test Corp",
        Url         = url,
        Description = description,
        Location    = "Melbourne, VIC",
        SalaryMin   = salaryMin,
        SalaryMax   = salaryMax,
        PublishedAt = publishedAt ?? DateTime.UtcNow,
        Source      = "greenhouse",
    };

    private static PostingEvaluation StubEval(string rec) => new()
    {
        Company = "Test Corp", RoleTitle = "Software Engineer", Recommendation = rec,
        SponsorshipVerdict = "pass", LocationMatch = "preferred", LocationDetail = "Melbourne",
        ExperienceMatch = "ideal", ExperienceDetail = "3+ years",
        BackendMatch = "strong", BackendTechnologies = ["C#"],
        FrontendMatch = "acceptable", FrontendTechnologies = [],
        SalaryAssessment = "missing", CompanyAssessment = "preferred",
        RoleTypeMatch = "preferred", OrangeFlags = [], Rationale = "Fine.",
    };

    private static JobDiscoveryWorker MakeWorker(
        AppDbContext db,
        IEnumerable<IJobFetcher>? fetchers = null,
        JobPostingFetcher? pageFetcher = null,
        PostingEvaluator? evaluator = null,
        TelegramNotifier? telegram = null) =>
        new(db,
            fetchers   ?? [],
            pageFetcher ?? new FakePageFetcher(_ => "page text"),
            evaluator  ?? new FakeEval(_ => StubEval("weak_match")),
            telegram);

    private sealed class FakeFetcher(List<JobFeedItem> items) : IJobFetcher
    {
        public Task<List<JobFeedItem>> FetchAllAsync() => Task.FromResult(items);
    }

    private sealed class FakePageFetcher : JobPostingFetcher
    {
        private readonly Func<string, string> _fn;
        public FakePageFetcher(Func<string, string> fn) => _fn = fn;
        public override Task<string> FetchAsync(string url) => Task.FromResult(_fn(url));
    }

    private sealed class FakeEval : PostingEvaluator
    {
        private readonly Func<string, PostingEvaluation> _fn;
        public FakeEval(Func<string, PostingEvaluation> fn) : base() => _fn = fn;
        public override Task<PostingEvaluation> EvaluateAsync(string postingText, string? sourceUrl = null)
            => Task.FromResult(_fn(postingText));
    }

    private sealed class FakeTelegram : TelegramNotifier
    {
        private readonly bool _returns;
        public int CallCount { get; private set; }
        public FakeTelegram(bool returns = true) : base("x", "y") => _returns = returns;
        public override Task<bool> SendAsync(string message, string? parseMode = null)
        {
            CallCount++;
            return Task.FromResult(_returns);
        }
    }
}
