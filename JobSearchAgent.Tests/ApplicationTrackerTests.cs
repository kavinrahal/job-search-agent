using JobSearch.Data;
using JobSearchAgent.Agents;
using Microsoft.EntityFrameworkCore;

namespace JobSearchAgent.Tests;

public class ApplicationTrackerTests
{
    // TC01
    [Fact]
    public async Task NonJobRelatedEmail_ProcessClassifications_ReturnsZeroCounters()
    {
        var db = Db.Fresh();
        var email = Make.Email();
        var clf = Make.Classification(isJobRelated: false, company: "Acme", category: "application_confirmation");

        var result = await ApplicationTracker.ProcessClassificationsAsync(db, [Fixtures.Pair(email, clf)]);

        Assert.Equal(0, result.Created);
        Assert.Equal(0, result.Updated);
        Assert.Equal(0, result.NotificationsQueued);
        Assert.Empty(db.Applications.ToList());
    }

    // TC02
    [Fact]
    public async Task RecruiterOutreachEmail_ProcessClassifications_SkipsEvenWhenJobRelated()
    {
        var db = Db.Fresh();
        var email = Make.Email();
        var clf = Make.Classification(isJobRelated: true, category: "recruiter_outreach", company: "Acme");

        var result = await ApplicationTracker.ProcessClassificationsAsync(db, [Fixtures.Pair(email, clf)]);

        Assert.Equal(0, result.Created);
        Assert.Empty(db.Applications.ToList());
    }

    // TC03
    [Fact]
    public async Task WhitespaceCompany_ProcessClassifications_SkipsWithNoDbWrite()
    {
        var db = Db.Fresh();
        var email = Make.Email();
        var clf = Make.Classification(isJobRelated: true, category: "application_confirmation", company: "   ");

        await ApplicationTracker.ProcessClassificationsAsync(db, [Fixtures.Pair(email, clf)]);

        Assert.Empty(db.Applications.ToList());
    }

    // TC04
    // The tracker creates the app at Applied, then immediately resolves application_confirmation
    // which advances Applied→Acknowledged, writing a second StatusChanged event.
    // The creation event has FromStatus=null, ToStatus=Applied.
    [Fact]
    public async Task ApplicationConfirmation_NoExistingApp_CreatesWithStatusApplied()
    {
        var db = Db.Fresh();
        var email = Make.Email();
        var clf = Make.Classification(category: "application_confirmation", company: "Acme", roleTitle: "Engineer");

        await ApplicationTracker.ProcessClassificationsAsync(db, [Fixtures.Pair(email, clf)]);

        var app = db.Applications.First();
        var events = db.ApplicationEvents.Where(e => e.ApplicationId == app.Id).OrderBy(e => e.Id).ToList();

        // First event is the creation event: null → Applied
        var creationEvent = events.First();
        Assert.Null(creationEvent.FromStatus);
        Assert.Equal(ApplicationStatus.Applied, creationEvent.ToStatus);
    }

    // TC05
    [Fact]
    public async Task FollowUpNeeded_NoExistingApp_CreatesNoApplicationAndQueuesNoNotification()
    {
        var db = Db.Fresh();
        var email = Make.Email();
        var clf = Make.Classification(category: "follow_up_needed");

        var result = await ApplicationTracker.ProcessClassificationsAsync(db, [Fixtures.Pair(email, clf)]);

        Assert.Equal(0, result.Created);
        Assert.Equal(0, result.NotificationsQueued);
        Assert.Empty(db.Applications.ToList());
    }

    // TC06
    [Fact]
    public async Task CaseInsensitiveCompany_FindOrCreate_MatchesExistingApplication()
    {
        var db = Db.Fresh();
        Seed.Application(db, company: "ACME", roleTitle: "Engineer", status: ApplicationStatus.Applied);

        var email = Make.Email();
        var clf = Make.Classification(category: "interview_invitation", company: "acme", roleTitle: "Engineer");

        await ApplicationTracker.ProcessClassificationsAsync(db, [Fixtures.Pair(email, clf)]);

        Assert.Equal(1, db.Applications.Count());
        Assert.Equal("ACME", db.Applications.First().Company); // original entity preserved, not recreated with different casing
    }

    // TC07
    [Fact]
    public async Task BlankRoleTitle_FindOrCreate_MatchesExistingAppWithAnyRole()
    {
        var db = Db.Fresh();
        Seed.Application(db, company: "Acme", roleTitle: "Senior Engineer", status: ApplicationStatus.Applied);

        var email = Make.Email();
        var clf = Make.Classification(category: "interview_invitation", company: "Acme", roleTitle: "");

        await ApplicationTracker.ProcessClassificationsAsync(db, [Fixtures.Pair(email, clf)]);

        Assert.Equal(1, db.Applications.Count());
        var app = db.Applications.First();
        Assert.Equal(ApplicationStatus.Interviewing, app.Status);
    }

    // TC08
    [Fact]
    public async Task TerminalStatus_Rejection_NoStatusChangeAndEmailReceivedEventWritten()
    {
        var db = Db.Fresh();
        Seed.Application(db, company: "Acme", status: ApplicationStatus.Rejected);

        var email = Make.Email();
        var clf = Make.Classification(category: "rejection", company: "Acme");

        await ApplicationTracker.ProcessClassificationsAsync(db, [Fixtures.Pair(email, clf)]);

        var app = db.Applications.First();
        Assert.Equal(ApplicationStatus.Rejected, app.Status);

        var newEvent = db.ApplicationEvents
            .Where(e => e.ApplicationId == app.Id)
            .OrderByDescending(e => e.Id)
            .First();
        Assert.Equal(ApplicationEventType.EmailReceived, newEvent.EventType);
    }

    // TC09
    [Fact]
    public async Task LowerRankTransition_CanAdvanceTo_BlocksRegression()
    {
        var db = Db.Fresh();
        Seed.Application(db, company: "Acme", status: ApplicationStatus.Interviewing);

        var email = Make.Email();
        var clf = Make.Classification(category: "application_confirmation", company: "Acme");

        await ApplicationTracker.ProcessClassificationsAsync(db, [Fixtures.Pair(email, clf)]);

        var app = db.Applications.First();
        Assert.Equal(ApplicationStatus.Interviewing, app.Status);
    }

    // TC10
    [Fact]
    public async Task StatusAdvance_ExistingApp_IncrementsUpdatedNotCreated()
    {
        var db = Db.Fresh();
        Seed.Application(db, company: "Acme", roleTitle: "Engineer", status: ApplicationStatus.Applied);

        var email = Make.Email();
        var clf = Make.Classification(category: "interview_invitation", company: "Acme", roleTitle: "Engineer");

        var result = await ApplicationTracker.ProcessClassificationsAsync(db, [Fixtures.Pair(email, clf)]);

        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Updated);
    }

    // TC11
    [Fact]
    public async Task NewAppWithAdvancingCategory_DoesNotIncrementUpdated()
    {
        var db = Db.Fresh();
        var email = Make.Email();
        var clf = Make.Classification(category: "interview_invitation");

        var result = await ApplicationTracker.ProcessClassificationsAsync(db, [Fixtures.Pair(email, clf)]);

        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Updated);
    }

    // TC12
    [Fact]
    public async Task SubjectOver100Chars_BuildMessage_TruncatesToExactly100()
    {
        var db = Db.Fresh();
        var email = Make.Email(subject: new string('a', 150));
        var clf = Make.Classification(category: "interview_invitation", company: "Acme");

        await ApplicationTracker.ProcessClassificationsAsync(db, [Fixtures.Pair(email, clf)]);

        var notification = db.Notifications.First();
        var subjectLine = notification.Message.Split('\n').Last();
        Assert.Equal(100, subjectLine.Length);
    }

    // TC13
    [Fact]
    public async Task ApplicationConfirmation_ExistingAppNotAtApplied_ProducesEmailReceivedNotStatusChange()
    {
        var db = Db.Fresh();
        Seed.Application(db, company: "Acme", status: ApplicationStatus.Interviewing);

        var email = Make.Email();
        var clf = Make.Classification(category: "application_confirmation", company: "Acme");

        await ApplicationTracker.ProcessClassificationsAsync(db, [Fixtures.Pair(email, clf)]);

        var app = db.Applications.First();
        Assert.Equal(ApplicationStatus.Interviewing, app.Status);

        var newEvent = db.ApplicationEvents
            .Where(e => e.ApplicationId == app.Id)
            .OrderByDescending(e => e.Id)
            .First();
        Assert.Equal(ApplicationEventType.EmailReceived, newEvent.EventType);
    }

    // TC14
    [Fact]
    public async Task SchedulingRequest_AppAtScreening_NoStatusChange()
    {
        var db = Db.Fresh();
        Seed.Application(db, company: "Acme", status: ApplicationStatus.Screening);

        var email = Make.Email();
        var clf = Make.Classification(category: "scheduling_request", company: "Acme");

        await ApplicationTracker.ProcessClassificationsAsync(db, [Fixtures.Pair(email, clf)]);

        var app = db.Applications.First();
        Assert.Equal(ApplicationStatus.Screening, app.Status);

        var newEvent = db.ApplicationEvents
            .Where(e => e.ApplicationId == app.Id)
            .OrderByDescending(e => e.Id)
            .First();
        Assert.Equal(ApplicationEventType.EmailReceived, newEvent.EventType);
    }

    // TC15
    [Fact]
    public async Task InterviewInvitation_NoExistingApp_QueuesNotification()
    {
        var db = Db.Fresh();
        var email = Make.Email();
        var clf = Make.Classification(category: "interview_invitation");

        var result = await ApplicationTracker.ProcessClassificationsAsync(db, [Fixtures.Pair(email, clf)]);

        Assert.Equal(1, result.NotificationsQueued);
    }

    // TC16
    [Fact]
    public async Task ApplicationConfirmation_NoExistingApp_DoesNotQueueNotification()
    {
        var db = Db.Fresh();
        var email = Make.Email();
        var clf = Make.Classification(category: "application_confirmation");

        var result = await ApplicationTracker.ProcessClassificationsAsync(db, [Fixtures.Pair(email, clf)]);

        Assert.Equal(0, result.NotificationsQueued);
    }

    // TC17
    // application_confirmation on a new app writes exactly two StatusChanged events:
    // (null→Applied) from FindOrCreate, then (Applied→Acknowledged) from ProcessClassifications.
    [Fact]
    public async Task NewApp_ApplicationConfirmation_WritesTwoStatusChangedEvents()
    {
        var db = Db.Fresh();
        var email = Make.Email();
        var clf = Make.Classification(category: "application_confirmation");

        await ApplicationTracker.ProcessClassificationsAsync(db, [Fixtures.Pair(email, clf)]);

        var app = db.Applications.First();
        var events = db.ApplicationEvents.Where(e => e.ApplicationId == app.Id).OrderBy(e => e.Id).ToList();
        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.Equal(ApplicationEventType.StatusChanged, e.EventType));
    }

    // TC18
    [Fact]
    public async Task OfferOnTerminalRejectedApp_StatusUnchangedAndNotificationStillQueued()
    {
        var db = Db.Fresh();
        Seed.Application(db, company: "Acme", status: ApplicationStatus.Rejected);

        var email = Make.Email();
        var clf = Make.Classification(category: "offer", company: "Acme");

        var result = await ApplicationTracker.ProcessClassificationsAsync(db, [Fixtures.Pair(email, clf)]);

        var app = db.Applications.First();
        Assert.Equal(ApplicationStatus.Rejected, app.Status);
        Assert.Equal(1, result.NotificationsQueued);
    }

    // TC19 — The dedup gap this closes: realistic LLM classification variance ("Acme Corp"
    // vs "Acme Corporation") wouldn't match on company name alone, but a manually-logged
    // filter-tracking-mode application carries the company's email domain, and the real
    // confirmation email's sender domain matches it — so this should update the existing
    // row, not create a duplicate.
    [Fact]
    public async Task DomainMatch_DifferentCompanyNameSameDomain_MatchesExistingApplication()
    {
        var db = Db.Fresh();
        Seed.Application(db, company: "Acme Corp", roleTitle: "", status: ApplicationStatus.Applied, companyDomain: "acmecorp.com");

        var email = Make.Email(fromAddress: "HR Team <hr@acmecorp.com>");
        var clf = Make.Classification(category: "interview_invitation", company: "Acme Corporation", roleTitle: "Engineer");

        var result = await ApplicationTracker.ProcessClassificationsAsync(db, [Fixtures.Pair(email, clf)]);

        Assert.Equal(1, db.Applications.Count()); // no duplicate created
        Assert.Equal(0, result.Created);
        Assert.Equal(ApplicationStatus.Interviewing, db.Applications.First().Status);
    }

    // TC20 — ExtractDomain in isolation, since it's a plain string-parsing helper that's easy
    // to get subtly wrong (off-by-one on the '@'/'<'/'>' indices) without it ever showing up
    // as a test failure elsewhere.
    [Theory]
    [InlineData("HR Team <hr@AcmeCorp.com>", "acmecorp.com")]
    [InlineData("hr@acmecorp.com", "acmecorp.com")]
    [InlineData("no-at-sign", null)]
    [InlineData("trailing-at@", null)]
    public void ExtractDomain_VariousFormats_ReturnsLowercasedDomainOrNull(string fromHeader, string? expected)
    {
        Assert.Equal(expected, ApplicationTracker.ExtractDomain(fromHeader));
    }
}
