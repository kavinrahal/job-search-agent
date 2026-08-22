using JobSearchAgent.Agents;
using JobSearchAgent.Models;
using JobSearchAgent.Workers;

namespace JobSearchAgent.Tests;

// This is the actual privacy boundary: get it wrong one way and irrelevant inbox content
// (personal mail, receipts, anything) is retained indefinitely; get it wrong the other way
// and JobAlertProcessor silently stops finding job URLs in alert digests it still needs to
// re-scan. Both failure directions are worth a real test, not just the happy path.
public class RawEmailRetentionPolicyTests
{
    private static RawEmail MakeEmail(string id) => new(id, "thread-1", "from@example.com", "Subject", "body", DateTimeOffset.UtcNow);

    private static EmailClassification Classify(bool jobRelated, string category) =>
        new() { IsJobRelated = jobRelated, Category = category };

    // TC01 — the overwhelming common case for any real inbox: most mail isn't job-related at
    // all, and none of it is ever read again after classification.
    [Fact]
    public void SelectMessageIdsToScrub_NotJobRelated_IsScrubbed()
    {
        var results = new[] { (MakeEmail("m1"), Classify(false, "not_relevant")) };

        var scrubbed = RawEmailRetentionPolicy.SelectMessageIdsToScrub(results);

        Assert.Contains("m1", scrubbed);
    }

    // TC02 — job-related but already fully acted on (ApplicationTracker consumed it
    // in-memory, nothing re-reads RawEmails for these categories).
    [Theory]
    [InlineData("application_confirmation")]
    [InlineData("rejection")]
    [InlineData("interview_invitation")]
    [InlineData("recruiter_outreach")]
    public void SelectMessageIdsToScrub_ActedOnJobRelatedCategories_AreScrubbed(string category)
    {
        var results = new[] { (MakeEmail("m1"), Classify(true, category)) };

        var scrubbed = RawEmailRetentionPolicy.SelectMessageIdsToScrub(results);

        Assert.Contains("m1", scrubbed);
    }

    // TC03 — the one real exception: JobAlertProcessor re-reads stored job_alert emails on
    // every run to extract job URLs. Scrubbing these would silently break that re-scan with
    // no error anywhere — the worse of the two possible mistakes here.
    [Fact]
    public void SelectMessageIdsToScrub_JobAlert_IsNotScrubbed()
    {
        var results = new[] { (MakeEmail("m1"), Classify(true, "job_alert")) };

        var scrubbed = RawEmailRetentionPolicy.SelectMessageIdsToScrub(results);

        Assert.DoesNotContain("m1", scrubbed);
    }

    // TC04 — a realistic mixed batch, confirming each email is decided independently rather
    // than one classification leaking into another's outcome.
    [Fact]
    public void SelectMessageIdsToScrub_MixedBatch_DecidesEachIndependently()
    {
        var results = new[]
        {
            (MakeEmail("irrelevant"), Classify(false, "not_relevant")),
            (MakeEmail("alert"), Classify(true, "job_alert")),
            (MakeEmail("rejection"), Classify(true, "rejection")),
        };

        var scrubbed = RawEmailRetentionPolicy.SelectMessageIdsToScrub(results);

        Assert.Contains("irrelevant", scrubbed);
        Assert.Contains("rejection", scrubbed);
        Assert.DoesNotContain("alert", scrubbed);
    }
}
