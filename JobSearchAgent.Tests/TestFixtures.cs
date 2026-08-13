using JobSearch.Data;
using JobSearchAgent.Agents;
using JobSearchAgent.Models;
using Microsoft.EntityFrameworkCore;

namespace JobSearchAgent.Tests;

public static class Db
{
    // Every existing test predates multi-tenancy and implicitly runs as one tenant —
    // CurrentUserId defaults to this so query filters stay transparent to them.
    public const int TestUserId = 1;

    // dbName lets a test open a second, independently-tracked context against the same
    // underlying InMemory database — needed to simulate "a different request/tenant" rather
    // than reusing a context that already has the row in its local change tracker. Only the
    // first Fresh() call against a given dbName should seed the UserProfile row (a second
    // context opened against the same dbName would otherwise try to insert a duplicate).
    public static AppDbContext Fresh(string? dbName = null)
    {
        bool isNewDb = dbName is null;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options) { CurrentUserId = TestUserId };

        if (isNewDb)
        {
            // PostingEvaluator.EvaluateAsync (and CvTailorAgent/CoverLetterAgent/AnswerAgent)
            // now take a UserProfile — worker classes fetch it via CurrentUserId, so it must
            // exist for any test that exercises JobDiscoveryWorker/JobAlertProcessor.
            db.UserProfiles.Add(new UserProfile { UserId = TestUserId, Background = "Test background.", CvBase = "Test CV.", JobCriteria = "Test criteria." });
            db.SaveChanges();
        }

        return db;
    }
}

public static class Make
{
    public static EmailClassification Classification(
        bool isJobRelated = true,
        string category = "application_confirmation",
        string company = "Acme",
        string roleTitle = "Engineer",
        double confidence = 0.9) =>
        new()
        {
            IsJobRelated = isJobRelated,
            Category = category,
            Company = company,
            RoleTitle = roleTitle,
            Confidence = confidence,
        };

    // Loads the real context files, mirroring what the agent constructors used to load
    // once at startup — contract tests hit the real API and want realistic content.
    public static UserProfile OwnerProfile() => new()
    {
        UserId = Db.TestUserId,
        Background = SkillLoader.Load("context/background.yaml"),
        CvBase = SkillLoader.Load("context/cv_base.md"),
        JobCriteria = SkillLoader.Load("context/job_criteria.yaml"),
    };

    public static RawEmail Email(
        string messageId = "msg-1",
        string subject = "Test subject",
        DateTimeOffset receivedAt = default,
        string threadId = "thread-1",
        string fromAddress = "noreply@example.com",
        string bodyText = "") =>
        new(
            MessageId: messageId,
            ThreadId: threadId,
            FromAddress: fromAddress,
            Subject: subject,
            BodyText: bodyText,
            ReceivedAt: receivedAt == default ? DateTimeOffset.UtcNow : receivedAt
        );
}

public static class Seed
{
    public static Application Application(
        AppDbContext db,
        string company = "Acme",
        string roleTitle = "Engineer",
        string? status = null)
    {
        var app = new JobSearch.Data.Application
        {
            UserId = db.CurrentUserId ?? Db.TestUserId,
            Company = company,
            RoleTitle = roleTitle,
            Status = status ?? ApplicationStatus.Applied,
            AppliedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Applications.Add(app);
        db.SaveChanges();
        return app;
    }
}

public static class Fixtures
{
    public static (RawEmail Email, EmailClassification Classification) Pair(
        RawEmail email,
        EmailClassification clf) => (email, clf);
}
