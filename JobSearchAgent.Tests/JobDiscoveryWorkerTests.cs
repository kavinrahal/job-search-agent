using JobSearch.Data;
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

        var result = item.ToPostingText();

        Assert.Contains("$100,000", result);
        Assert.Contains("$140,000", result);
        Assert.Contains("AUD", result);
    }

    // TC02 — Only min salary → "From $X AUD"
    [Fact]
    public void FormatPostingText_OnlyMinSalary_FromFormat()
    {
        var item = FeedItem(salaryMin: 90_000, salaryMax: null);

        var result = item.ToPostingText();

        Assert.Contains("From $90,000 AUD", result);
    }

    // TC03 — No salary fields → "Not stated"
    // Silent failure: missing null-guard would produce empty salary line, confusing the evaluator.
    [Fact]
    public void FormatPostingText_NoSalary_NotStated()
    {
        var item = FeedItem(salaryMin: null, salaryMax: null);

        var result = item.ToPostingText();

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

        var (discovered, evaluated, notified) = await worker.RunAsync();

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
            UserId = Db.TestUserId,
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

    // TC05b — Two different users discovering the same URL each get their own row.
    // Silent failure: a global (not per-user) unique index on Url would make the second
    // user's insert throw a duplicate-key exception even though the per-user dedup query
    // correctly treats the URL as new for them — this is the exact bug this ticket fixes.
    [Fact]
    public async Task RunAsync_SameUrlDiscoveredByTwoDifferentUsers_EachGetsOwnRow()
    {
        var dbName = Guid.NewGuid().ToString();
        var item = FeedItem();

        var dbUser1 = Db.Fresh(dbName);
        dbUser1.UserProfiles.Add(new UserProfile { UserId = Db.TestUserId, Background = "b", CvBase = "c", JobCriteria = "j" });
        dbUser1.SaveChanges();
        var worker1 = MakeWorker(dbUser1, fetchers: [new FakeFetcher([item])], evaluator: new FakeEval(_ => StubEval("strong_match")));
        await worker1.RunAsync();

        var dbUser2 = Db.Fresh(dbName);
        dbUser2.CurrentUserId = 2;
        dbUser2.UserProfiles.Add(new UserProfile { UserId = 2, Background = "b", CvBase = "c", JobCriteria = "j" });
        dbUser2.SaveChanges();
        var worker2 = MakeWorker(dbUser2, fetchers: [new FakeFetcher([item])], evaluator: new FakeEval(_ => StubEval("weak_match")));
        var (discovered, evaluated, _) = await worker2.RunAsync();

        Assert.Equal(1, discovered); // not skipped as a duplicate — new for user 2
        Assert.Equal(1, evaluated);

        var user1Record = dbUser1.DiscoveredPostings.Single(d => d.Url == item.Url);
        var user2Record = dbUser2.DiscoveredPostings.Single(d => d.Url == item.Url);
        Assert.Equal("strong_match", user1Record.Recommendation);
        Assert.Equal("weak_match", user2Record.Recommendation);
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

    // TC09b — Errored posting under the dead-letter cap (FailureCount < 3) is still picked up
    // and retried — no behavior change for a posting that's failed once or twice.
    [Fact]
    public async Task RunAsync_ErroredPostingUnderCap_StillRetried()
    {
        var db = Db.Fresh();
        var item = FeedItem();
        db.DiscoveredPostings.Add(new DiscoveredPosting
        {
            UserId = Db.TestUserId,
            Url = item.Url, Source = "greenhouse", Title = item.Title,
            Recommendation = "error", FailureCount = 2,
            DiscoveredAt = DateTime.UtcNow, EvaluatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        var worker = MakeWorker(db,
            fetchers: [new FakeFetcher([item])],
            evaluator: new FakeEval(_ => StubEval("weak_match")));

        var (discovered, evaluated, _) = await worker.RunAsync();

        Assert.Equal(1, discovered);
        Assert.Equal(1, evaluated);
        var record = db.DiscoveredPostings.Single(d => d.Url == item.Url);
        Assert.Equal("weak_match", record.Recommendation);
    }

    // TC09c — Errored posting that has hit the dead-letter cap (FailureCount == 3) is excluded
    // from the dedup query entirely — never retried again.
    [Fact]
    public async Task RunAsync_ErroredPostingAtCap_ExcludedFromRetry()
    {
        var db = Db.Fresh();
        var item = FeedItem();
        db.DiscoveredPostings.Add(new DiscoveredPosting
        {
            UserId = Db.TestUserId,
            Url = item.Url, Source = "greenhouse", Title = item.Title,
            Recommendation = "error", FailureCount = 3,
            DiscoveredAt = DateTime.UtcNow, EvaluatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        int evalCount = 0;
        var worker = MakeWorker(db,
            fetchers: [new FakeFetcher([item])],
            evaluator: new FakeEval(_ => { evalCount++; return StubEval("weak_match"); }));

        var (discovered, evaluated, _) = await worker.RunAsync();

        Assert.Equal(0, discovered);
        Assert.Equal(0, evaluated);
        Assert.Equal(0, evalCount);
    }

    // TC09d — FailureCount increments by exactly 1 per failed run, and stops changing once
    // the posting has hit the cap and is excluded from future runs.
    [Fact]
    public async Task RunAsync_RepeatedFailures_FailureCountIncrementsEachRun()
    {
        var db = Db.Fresh();
        var item = FeedItem();
        var worker = MakeWorker(db,
            fetchers: [new FakeFetcher([item])],
            evaluator: new FakeEval(_ => throw new InvalidOperationException("LLM down")));

        await worker.RunAsync();
        Assert.Equal(1, db.DiscoveredPostings.Single(d => d.Url == item.Url).FailureCount);

        await worker.RunAsync();
        Assert.Equal(2, db.DiscoveredPostings.Single(d => d.Url == item.Url).FailureCount);

        await worker.RunAsync();
        Assert.Equal(3, db.DiscoveredPostings.Single(d => d.Url == item.Url).FailureCount);

        // Now at the cap — a further run must not touch it at all.
        await worker.RunAsync();
        Assert.Equal(3, db.DiscoveredPostings.Single(d => d.Url == item.Url).FailureCount);
    }

    // TC09e — A posting that fails, then later succeeds, has its FailureCount reset to 0 —
    // an old failure streak shouldn't count against a posting that's evaluating fine now.
    [Fact]
    public async Task RunAsync_SuccessAfterFailure_ResetsFailureCountToZero()
    {
        var db = Db.Fresh();
        var item = FeedItem();
        db.DiscoveredPostings.Add(new DiscoveredPosting
        {
            UserId = Db.TestUserId,
            Url = item.Url, Source = "greenhouse", Title = item.Title,
            Recommendation = "error", FailureCount = 2,
            DiscoveredAt = DateTime.UtcNow, EvaluatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        var worker = MakeWorker(db,
            fetchers: [new FakeFetcher([item])],
            evaluator: new FakeEval(_ => StubEval("weak_match")));

        await worker.RunAsync();

        Assert.Equal(0, db.DiscoveredPostings.Single(d => d.Url == item.Url).FailureCount);
    }

    // TC10 — strong_match with an emailer configured → EmailNotificationSent=true, notified=1
    [Fact]
    public async Task RunAsync_StrongMatchWithEmailer_EmailNotificationSent()
    {
        var db = Db.Fresh();
        db.Users.Add(new User { Id = Db.TestUserId, Email = "owner@test.com" });
        db.SaveChanges();
        var handler = new FakeEmailHandler();
        var worker = MakeWorker(db,
            fetchers: [new FakeFetcher([FeedItem()])],
            evaluator: new FakeEval(_ => StubEval("strong_match")),
            emailer: Emailer.Make(handler));

        var (_, _, notified) = await worker.RunAsync();

        var record = db.DiscoveredPostings.Single();
        Assert.True(record.EmailNotificationSent);
        Assert.Equal(1, notified);
        Assert.Equal(1, handler.CallCount);
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
        SkillMatches = [new SkillMatch("Backend stack", "strong", "C#")],
        SalaryAssessment = "missing", CompanyAssessment = "preferred",
        RoleTypeMatch = "preferred", OrangeFlags = [], Rationale = "Fine.",
    };

    private static JobDiscoveryWorker MakeWorker(
        AppDbContext db,
        IEnumerable<IJobFetcher>? fetchers = null,
        JobPostingFetcher? pageFetcher = null,
        PostingEvaluator? evaluator = null,
        SendGridEmailService? emailer = null) =>
        new(db,
            fetchers   ?? [],
            pageFetcher ?? new FakePageFetcher(_ => "page text"),
            evaluator  ?? new FakeEval(_ => StubEval("weak_match")),
            emailer);

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
        public override Task<PostingEvaluation> EvaluateAsync(UserProfile profile, string postingText, string? sourceUrl = null)
            => Task.FromResult(_fn(postingText));
    }
}
