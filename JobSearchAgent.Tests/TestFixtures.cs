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
    // than reusing a context that already has the row in its local change tracker.
    public static AppDbContext Fresh(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options) { CurrentUserId = TestUserId };
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
