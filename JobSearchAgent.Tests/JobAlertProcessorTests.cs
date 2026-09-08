using JobSearch.Data;
using JobSearchAgent.Workers;

namespace JobSearchAgent.Tests;

public class JobAlertProcessorTests
{
    // TC01 — Seek www.seek.com.au variant normalises to au.seek.com
    // Silent failure: wrong regex branch silently drops all www.seek.com.au alert emails.
    [Fact]
    public void ExtractJobUrls_SeekWwwVariant_NormalisedToAuSeek()
    {
        var email = Make.Email(bodyText: "Apply at https://www.seek.com.au/job/11223344");

        var result = JobAlertProcessor.ExtractJobUrls([email]);

        Assert.True(result.ContainsKey("https://au.seek.com/job/11223344"));
        Assert.Equal("seek_alert", result["https://au.seek.com/job/11223344"]);
    }

    // TC02 — Seek au.seek.com variant (the other regex alternation branch)
    // Silent failure: same bug for the au.seek.com branch.
    [Fact]
    public void ExtractJobUrls_SeekAuVariant_NormalisedToAuSeek()
    {
        var email = Make.Email(bodyText: "Apply at https://au.seek.com/job/99887766");

        var result = JobAlertProcessor.ExtractJobUrls([email]);

        Assert.True(result.ContainsKey("https://au.seek.com/job/99887766"));
    }

    // TC03 — LinkedIn URL extracted with correct source label
    [Fact]
    public void ExtractJobUrls_LinkedInUrl_ExtractedWithCorrectSource()
    {
        var email = Make.Email(bodyText: "View: https://www.linkedin.com/jobs/view/3456789012/");

        var result = JobAlertProcessor.ExtractJobUrls([email]);

        Assert.True(result.ContainsKey("https://www.linkedin.com/jobs/view/3456789012"));
        Assert.Equal("linkedin_alert", result["https://www.linkedin.com/jobs/view/3456789012"]);
    }

    // TC04 — Jora URL with mixed-case alphanumeric slug
    // Silent failure: slug regex `[A-Za-z0-9_-]+` truncates or misses if wrong charset.
    [Fact]
    public void ExtractJobUrls_JoraUrl_ExtractedWithCorrectSource()
    {
        var email = Make.Email(bodyText: "Job at https://au.jora.com/job/AbC123-xyz_9");

        var result = JobAlertProcessor.ExtractJobUrls([email]);

        Assert.True(result.ContainsKey("https://au.jora.com/job/AbC123-xyz_9"));
        Assert.Equal("jora_alert", result["https://au.jora.com/job/AbC123-xyz_9"]);
    }

    // TC05 — Same URL appearing in two separate emails deduplicates to one entry
    // Silent failure: if TryAdd is replaced with indexer assignment, last-wins overwrites source label.
    [Fact]
    public void ExtractJobUrls_DuplicateUrlAcrossEmails_SingleEntry()
    {
        var url = "https://au.seek.com/job/55555555";
        var e1 = Make.Email(messageId: "m1", bodyText: $"Job: {url}?ref=seek1");
        var e2 = Make.Email(messageId: "m2", bodyText: $"Job: {url}?ref=seek2");

        var result = JobAlertProcessor.ExtractJobUrls([e1, e2]);

        Assert.Single(result);
    }

    // TC06 — Query params on a Seek URL are NOT included in the normalised key
    // Silent failure: using m.Value instead of m.Groups[1].Value bleeds query params into the
    // stored URL, breaking deduplication across emails with different tracking params.
    [Fact]
    public void ExtractJobUrls_SeekUrlWithQueryParams_NormalisedKeyHasNoQueryString()
    {
        var email = Make.Email(bodyText: "See https://au.seek.com/job/12345678?ref=email&tracking=abc");

        var result = JobAlertProcessor.ExtractJobUrls([email]);

        var key = Assert.Single(result).Key;
        Assert.DoesNotContain("?", key);
        Assert.Equal("https://au.seek.com/job/12345678", key);
    }

    // TC07 — Empty email list returns empty dictionary
    [Fact]
    public void ExtractJobUrls_EmptyEmailList_ReturnsEmpty()
    {
        var result = JobAlertProcessor.ExtractJobUrls([]);

        Assert.Empty(result);
    }

    // TC08 — Email body with no recognisable job URLs returns empty dictionary
    [Fact]
    public void ExtractJobUrls_NoMatchingUrls_ReturnsEmpty()
    {
        var email = Make.Email(bodyText: "Visit https://example.com or https://au.jora.com (no /job/ path)");

        var result = JobAlertProcessor.ExtractJobUrls([email]);

        Assert.Empty(result);
    }

    // =========================================================================
    // ProcessAsync
    // =========================================================================

    // TC-P1 — No job URLs in emails → early return, (0, 0, 0)
    [Fact]
    public async Task ProcessAsync_NoUrlsInEmails_ReturnsZeroTuple()
    {
        var db = Db.Fresh();
        var email = Make.Email(bodyText: "Nothing interesting here.");
        var processor = MakeProcessor(db);

        var (found, evaluated, notified) = await processor.ProcessAsync([email]);

        Assert.Equal(0, found);
        Assert.Equal(0, evaluated);
        Assert.Equal(0, notified);
    }

    // TC-P2 — URL already evaluated (non-error rec) → excluded from processing
    // Silent failure: broken dedup filter causes every posting to be re-evaluated on each cron run.
    [Fact]
    public async Task ProcessAsync_UrlAlreadyEvaluated_Skipped()
    {
        var db = Db.Fresh();
        var url = "https://au.seek.com/job/10000001";
        db.DiscoveredPostings.Add(new DiscoveredPosting
        {
            UserId = Db.TestUserId,
            Url = url, Source = "seek_alert", Title = "Dev", Recommendation = "good_match",
            DiscoveredAt = DateTime.UtcNow, EvaluatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();

        int evalCallCount = 0;
        var processor = MakeProcessor(db, evaluator: new FakeEvaluator(_ =>
        {
            evalCallCount++;
            return StubEval("weak_match");
        }));
        var email = Make.Email(bodyText: $"Job: {url}");

        var (found, evaluated, _) = await processor.ProcessAsync([email]);

        Assert.Equal(1, found);
        Assert.Equal(0, evaluated);
        Assert.Equal(0, evalCallCount);
    }

    // TC-P2b — Two different users' alert emails referencing the same URL each get their own row.
    // Silent failure: a global (not per-user) unique index on Url would make the second user's
    // insert throw a duplicate-key exception even though the per-user dedup query correctly
    // treats the URL as new for them — this is the exact bug this ticket fixes.
    [Fact]
    public async Task ProcessAsync_SameUrlAcrossTwoDifferentUsers_EachGetsOwnRow()
    {
        var dbName = Guid.NewGuid().ToString();
        var url = "https://au.seek.com/job/30000004";
        var email = Make.Email(bodyText: $"Job: {url}");

        var dbUser1 = Db.Fresh(dbName);
        dbUser1.UserProfiles.Add(new UserProfile { UserId = Db.TestUserId, Background = "b", CvBase = "c", JobCriteria = "j" });
        dbUser1.SaveChanges();
        var processor1 = MakeProcessor(dbUser1, evaluator: new FakeEvaluator(_ => StubEval("strong_match")));
        await processor1.ProcessAsync([email]);

        var dbUser2 = Db.Fresh(dbName);
        dbUser2.CurrentUserId = 2;
        dbUser2.UserProfiles.Add(new UserProfile { UserId = 2, Background = "b", CvBase = "c", JobCriteria = "j" });
        dbUser2.SaveChanges();
        var processor2 = MakeProcessor(dbUser2, evaluator: new FakeEvaluator(_ => StubEval("weak_match")));
        var (found, evaluated, _) = await processor2.ProcessAsync([email]);

        Assert.Equal(1, found);
        Assert.Equal(1, evaluated); // not skipped as a duplicate — new for user 2

        var user1Record = dbUser1.DiscoveredPostings.Single(d => d.Url == url);
        var user2Record = dbUser2.DiscoveredPostings.Single(d => d.Url == url);
        Assert.Equal("strong_match", user1Record.Recommendation);
        Assert.Equal("weak_match", user2Record.Recommendation);
    }

    // TC-P3 — URL with "error" recommendation IS included and retried; fields reset before eval
    // Silent failure: if error records aren't reset, a transient 403 permanently blocks a posting.
    [Fact]
    public async Task ProcessAsync_ErrorUrl_RetriedAndEvalResultPersisted()
    {
        var db = Db.Fresh();
        var url = "https://au.seek.com/job/20000002";
        db.DiscoveredPostings.Add(new DiscoveredPosting
        {
            UserId = Db.TestUserId,
            Url = url, Source = "seek_alert", Title = "",
            Recommendation = "error", DiscoveredAt = DateTime.UtcNow,
            EvaluatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();

        var processor = MakeProcessor(db, evaluator: new FakeEvaluator(_ => StubEval("weak_match")));
        var email = Make.Email(bodyText: $"Job: {url}");

        await processor.ProcessAsync([email]);

        var record = db.DiscoveredPostings.Single(d => d.Url == url);
        Assert.Equal("weak_match", record.Recommendation);
        Assert.NotNull(record.EvaluatedAt);
    }

    // TC-P4 — New URL → DiscoveredPosting created, eval result persisted with Company and Recommendation
    [Fact]
    public async Task ProcessAsync_NewUrl_RecordCreatedAndEvalPersisted()
    {
        var db = Db.Fresh();
        var url = "https://au.seek.com/job/30000003";
        var processor = MakeProcessor(db, evaluator: new FakeEvaluator(_ => StubEval("strong_match", company: "Atlassian")));
        var email = Make.Email(bodyText: $"Job: {url}");

        await processor.ProcessAsync([email]);

        var record = db.DiscoveredPostings.Single(d => d.Url == url);
        Assert.Equal("strong_match", record.Recommendation);
        Assert.Equal("Atlassian", record.Company);
        Assert.NotNull(record.EvaluatedAt);
    }

    // TC-P5 — Fetch throws but URL is in email fallback context → evaluation proceeds, no error record
    // Silent failure: if the when-guard is missing, the exception propagates and the posting gets
    // marked "error" even though the email body has enough information to evaluate it.
    [Fact]
    public async Task ProcessAsync_FetchThrowsWithFallback_EvalProceedsNoErrorRecord()
    {
        var db = Db.Fresh();
        var url = "https://au.seek.com/job/40000004";
        string? capturedText = null;
        var processor = MakeProcessor(
            db,
            fetcher: new FakeFetcher(_ => Task.FromException<string>(new HttpRequestException("DNS failure"))),
            evaluator: new FakeEvaluator(text =>
            {
                capturedText = text;
                return StubEval("good_match");
            }));
        var email = Make.Email(bodyText: $"Senior Engineer at ACME. Apply: {url}");

        await processor.ProcessAsync([email]);

        var record = db.DiscoveredPostings.Single(d => d.Url == url);
        Assert.NotEqual("error", record.Recommendation);
        Assert.NotNull(capturedText);
        Assert.Contains("ACME", capturedText);
    }

    // TC-P6 — Evaluator throws → outer catch sets Recommendation="error", loop continues
    // Silent failure: unhandled exception would terminate the loop, skipping all remaining URLs.
    [Fact]
    public async Task ProcessAsync_EvaluatorThrows_RecommendationSetToError()
    {
        var db = Db.Fresh();
        var url = "https://au.seek.com/job/50000005";
        var processor = MakeProcessor(db, evaluator: new FakeEvaluator(_ =>
        {
            throw new InvalidOperationException("LLM unavailable");
        }));
        var email = Make.Email(bodyText: $"Job: {url}");

        await processor.ProcessAsync([email]);

        var record = db.DiscoveredPostings.Single(d => d.Url == url);
        Assert.Equal("error", record.Recommendation);
    }

    // TC-P6b — Errored URL under the dead-letter cap (FailureCount < 3) is still picked up
    // and retried — no behavior change for a posting that's failed once or twice.
    [Fact]
    public async Task ProcessAsync_ErroredUrlUnderCap_StillRetried()
    {
        var db = Db.Fresh();
        var url = "https://au.seek.com/job/51000001";
        db.DiscoveredPostings.Add(new DiscoveredPosting
        {
            UserId = Db.TestUserId,
            Url = url, Source = "seek_alert", Title = "",
            Recommendation = "error", FailureCount = 2,
            DiscoveredAt = DateTime.UtcNow, EvaluatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        var processor = MakeProcessor(db, evaluator: new FakeEvaluator(_ => StubEval("weak_match")));
        var email = Make.Email(bodyText: $"Job: {url}");

        var (found, evaluated, _) = await processor.ProcessAsync([email]);

        Assert.Equal(1, found);
        Assert.Equal(1, evaluated);
        var record = db.DiscoveredPostings.Single(d => d.Url == url);
        Assert.Equal("weak_match", record.Recommendation);
    }

    // TC-P6c — Errored URL that has hit the dead-letter cap (FailureCount == 3) is excluded
    // from the dedup query entirely — never retried again.
    [Fact]
    public async Task ProcessAsync_ErroredUrlAtCap_ExcludedFromRetry()
    {
        var db = Db.Fresh();
        var url = "https://au.seek.com/job/51000002";
        db.DiscoveredPostings.Add(new DiscoveredPosting
        {
            UserId = Db.TestUserId,
            Url = url, Source = "seek_alert", Title = "",
            Recommendation = "error", FailureCount = 3,
            DiscoveredAt = DateTime.UtcNow, EvaluatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        int evalCallCount = 0;
        var processor = MakeProcessor(db, evaluator: new FakeEvaluator(_ =>
        {
            evalCallCount++;
            return StubEval("weak_match");
        }));
        var email = Make.Email(bodyText: $"Job: {url}");

        var (found, evaluated, _) = await processor.ProcessAsync([email]);

        Assert.Equal(1, found);
        Assert.Equal(0, evaluated);
        Assert.Equal(0, evalCallCount);
    }

    // TC-P6d — FailureCount increments by exactly 1 per failed run, and stops changing once
    // the posting has hit the cap and is excluded from future runs.
    [Fact]
    public async Task ProcessAsync_RepeatedFailures_FailureCountIncrementsEachRun()
    {
        var db = Db.Fresh();
        var url = "https://au.seek.com/job/51000003";
        var processor = MakeProcessor(db, evaluator: new FakeEvaluator(_ =>
            throw new InvalidOperationException("LLM unavailable")));
        var email = Make.Email(bodyText: $"Job: {url}");

        await processor.ProcessAsync([email]);
        Assert.Equal(1, db.DiscoveredPostings.Single(d => d.Url == url).FailureCount);

        await processor.ProcessAsync([email]);
        Assert.Equal(2, db.DiscoveredPostings.Single(d => d.Url == url).FailureCount);

        await processor.ProcessAsync([email]);
        Assert.Equal(3, db.DiscoveredPostings.Single(d => d.Url == url).FailureCount);

        // Now at the cap — a further run must not touch it at all.
        await processor.ProcessAsync([email]);
        Assert.Equal(3, db.DiscoveredPostings.Single(d => d.Url == url).FailureCount);
    }

    // TC-P6e — A URL that fails, then later succeeds, has its FailureCount reset to 0 — an
    // old failure streak shouldn't count against a posting that's evaluating fine now.
    [Fact]
    public async Task ProcessAsync_SuccessAfterFailure_ResetsFailureCountToZero()
    {
        var db = Db.Fresh();
        var url = "https://au.seek.com/job/51000004";
        db.DiscoveredPostings.Add(new DiscoveredPosting
        {
            UserId = Db.TestUserId,
            Url = url, Source = "seek_alert", Title = "",
            Recommendation = "error", FailureCount = 2,
            DiscoveredAt = DateTime.UtcNow, EvaluatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        var processor = MakeProcessor(db, evaluator: new FakeEvaluator(_ => StubEval("weak_match")));
        var email = Make.Email(bodyText: $"Job: {url}");

        await processor.ProcessAsync([email]);

        Assert.Equal(0, db.DiscoveredPostings.Single(d => d.Url == url).FailureCount);
    }

    // TC-P7 — strong_match with an emailer configured → EmailNotificationSent=true, notified=1
    // Silent failure: a broken notification flag means the same posting could be re-notified
    // on the next run if the record is ever reset.
    [Fact]
    public async Task ProcessAsync_StrongMatchWithEmailer_EmailNotificationSentAndCounted()
    {
        var db = Db.Fresh();
        db.Users.Add(new User { Id = Db.TestUserId, Email = "owner@test.com" });
        db.SaveChanges();
        var url = "https://au.seek.com/job/60000006";
        var handler = new FakeEmailHandler();
        var processor = MakeProcessor(db,
            evaluator: new FakeEvaluator(_ => StubEval("strong_match")),
            emailer: Emailer.Make(handler));
        var email = Make.Email(bodyText: $"Job: {url}");

        var (_, _, notified) = await processor.ProcessAsync([email]);

        var record = db.DiscoveredPostings.Single(d => d.Url == url);
        Assert.True(record.EmailNotificationSent);
        Assert.Equal(1, notified);
        Assert.Equal(1, handler.CallCount);
    }

    // TC-P8 — discard recommendation → no email send even when an emailer is provided
    [Fact]
    public async Task ProcessAsync_DiscardWithEmailer_EmailNotificationNotSent()
    {
        var db = Db.Fresh();
        db.Users.Add(new User { Id = Db.TestUserId, Email = "owner@test.com" });
        db.SaveChanges();
        var url = "https://au.seek.com/job/70000007";
        var handler = new FakeEmailHandler();
        var processor = MakeProcessor(db,
            evaluator: new FakeEvaluator(_ => StubEval("discard")),
            emailer: Emailer.Make(handler));
        var email = Make.Email(bodyText: $"Job: {url}");

        var (_, _, notified) = await processor.ProcessAsync([email]);

        Assert.Equal(0, notified);
        Assert.Equal(0, handler.CallCount);
    }

    // TC-P9 — Two new URLs: Found=2, Evaluated=2, Notified=0 (both weak_match, no emailer configured)
    [Fact]
    public async Task ProcessAsync_TwoNewUrls_CountsTupleCorrect()
    {
        var db = Db.Fresh();
        var processor = MakeProcessor(db, evaluator: new FakeEvaluator(_ => StubEval("weak_match")));
        var email = Make.Email(bodyText:
            "Job A: https://au.seek.com/job/80000008\n" +
            "Job B: https://au.seek.com/job/80000009");

        var (found, evaluated, notified) = await processor.ProcessAsync([email]);

        Assert.Equal(2, found);
        Assert.Equal(2, evaluated);
        Assert.Equal(0, notified);
    }

    // TC-P10 — Fetch fails, cross-check finds a confident match → evaluated on the matched
    // candidate's content, not the thin email snippet.
    // Silent failure: if the matched candidate is ignored, the whole cross-check feature is a
    // no-op and postings silently keep evaluating on the thin fallback text.
    [Fact]
    public async Task ProcessAsync_FetchFailsCrossCheckMatches_EvaluatesOnMatchedContent()
    {
        var db = Db.Fresh();
        var url = "https://au.seek.com/job/90000001";
        var matched = new JobFeedItem { Title = "Backend Engineer", Company = "Mintec Systems", Url = "https://au.jora.com/job/xyz", Description = "Real full description here.", Source = "jora" };
        string? capturedText = null;
        var processor = MakeProcessor(
            db,
            fetcher: new FakeFetcher(_ => Task.FromException<string>(new HttpRequestException("blocked"))),
            joraFetcher: new FakeJoraFetcher(_ => [matched]),
            matcher: new FakeMatcher((_, candidates) => candidates.Single()),
            evaluator: new FakeEvaluator(text => { capturedText = text; return StubEval("good_match"); }));
        var email = Make.Email(bodyText: $"Software Engineer\nMintec Systems\n\nMelbourne VIC\n\n{url}");

        await processor.ProcessAsync([email]);

        Assert.NotNull(capturedText);
        Assert.Contains("Real full description here.", capturedText);
    }

    // TC-P11 — Fetch fails, no candidates found anywhere → honest metadata-only fallback,
    // not an error record.
    [Fact]
    public async Task ProcessAsync_FetchFailsNoCrossCheckMatch_HonestFallbackNoError()
    {
        var db = Db.Fresh();
        var url = "https://au.seek.com/job/90000002";
        var processor = MakeProcessor(
            db,
            fetcher: new FakeFetcher(_ => Task.FromException<string>(new HttpRequestException("blocked"))),
            joraFetcher: new FakeJoraFetcher(_ => []));
        var email = Make.Email(bodyText: $"Software Engineer\nMintec Systems\n\nMelbourne VIC\n\n{url}");

        await processor.ProcessAsync([email]);

        var record = db.DiscoveredPostings.Single(d => d.Url == url);
        Assert.NotEqual("error", record.Recommendation);
    }

    // =========================================================================
    // ExtractSearchContext
    // =========================================================================

    // TC-C1 — Typical Seek alert layout (title/company blank-line location/salary blank-line url)
    // grabs the meaningful lines, skipping the blank lines between them.
    [Fact]
    public void ExtractSearchContext_TypicalAlertLayout_GrabsTitleCompanyLocation()
    {
        var url = "https://au.seek.com/job/93942243";
        var body = $"found 20 new jobs.\n\nSoftware Engineer - Java developer\nMintec Systems\n\nMelbourne VIC\n$110,000 – $130,000 per year\n\n[{url}]";

        var context = JobAlertProcessor.ExtractSearchContext(body, url);

        Assert.Contains("Software Engineer - Java developer", context);
        Assert.Contains("Mintec Systems", context);
        Assert.Contains("Melbourne VIC", context);
    }

    // TC-C2 — "logo" lines and bracketed image-URL lines between job blocks are skipped
    // Silent failure: without filtering, "logo" pollutes the search query sent to Jora/Adzuna.
    [Fact]
    public void ExtractSearchContext_LogoAndBracketLines_Skipped()
    {
        var url = "https://au.seek.com/job/1";
        var body = $"Full Stack Engineer\nAllume Energy\n\nlogo\n[https://cdn.example.com/logo.png]\n\n[{url}]";

        var context = JobAlertProcessor.ExtractSearchContext(body, url);

        Assert.DoesNotContain("logo", context, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cdn.example.com", context);
    }

    // TC-C3 — URL not present in the body → empty string, not an exception
    [Fact]
    public void ExtractSearchContext_UrlNotInBody_ReturnsEmpty()
    {
        var context = JobAlertProcessor.ExtractSearchContext("Nothing relevant here.", "https://au.seek.com/job/999");

        Assert.Equal("", context);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static JobAlertProcessor MakeProcessor(
        AppDbContext db,
        JobPostingFetcher? fetcher = null,
        PostingEvaluator? evaluator = null,
        JoraFetcher? joraFetcher = null,
        AdzunaFetcher? adzunaFetcher = null,
        PostingMatcherAgent? matcher = null,
        SendGridEmailService? emailer = null) =>
        new(db,
            fetcher   ?? new FakeFetcher(_ => Task.FromResult<string>("job description text")),
            evaluator ?? new FakeEvaluator(_ => StubEval("weak_match")),
            joraFetcher ?? new FakeJoraFetcher(_ => []),
            adzunaFetcher,
            matcher ?? new FakeMatcher((_, _) => null),
            emailer);

    private static PostingEvaluation StubEval(string recommendation, string company = "ACME") => new()
    {
        Company             = company,
        RoleTitle           = "Software Engineer",
        Recommendation      = recommendation,
        SponsorshipVerdict  = "pass",
        LocationMatch       = "preferred",
        LocationDetail      = "Melbourne",
        ExperienceMatch     = "ideal",
        ExperienceDetail    = "3+ years",
        SkillMatches        = [new SkillMatch("Backend stack", "strong", "C#")],
        SalaryAssessment    = "missing",
        CompanyAssessment   = "preferred",
        RoleTypeMatch       = "preferred",
        OrangeFlags         = [],
        Rationale           = "Test eval.",
    };

    private sealed class FakeFetcher : JobPostingFetcher
    {
        private readonly Func<string, Task<string>> _fn;
        public FakeFetcher(Func<string, Task<string>> fn) => _fn = fn;
        public override Task<string> FetchAsync(string url) => _fn(url);
    }

    private sealed class FakeEvaluator : PostingEvaluator
    {
        private readonly Func<string, PostingEvaluation> _fn;
        public FakeEvaluator(Func<string, PostingEvaluation> fn) : base() => _fn = fn;
        public override Task<PostingEvaluation> EvaluateAsync(UserProfile profile, string postingText, string? sourceUrl = null)
            => Task.FromResult(_fn(postingText));
    }

    private sealed class FakeJoraFetcher : JoraFetcher
    {
        private readonly Func<string, List<JobFeedItem>> _fn;
        public FakeJoraFetcher(Func<string, List<JobFeedItem>> fn) => _fn = fn;
        public override Task<List<JobFeedItem>> SearchAsync(string keywords, string location, string? company = null) =>
            Task.FromResult(_fn(keywords));
    }

    private sealed class FakeMatcher : PostingMatcherAgent
    {
        private readonly Func<string, IReadOnlyList<JobFeedItem>, JobFeedItem?> _fn;
        public FakeMatcher(Func<string, IReadOnlyList<JobFeedItem>, JobFeedItem?> fn) => _fn = fn;
        public override Task<JobFeedItem?> FindMatchAsync(int userId, string targetContext, IReadOnlyList<JobFeedItem> candidates) =>
            Task.FromResult(_fn(targetContext, candidates));
    }
}
