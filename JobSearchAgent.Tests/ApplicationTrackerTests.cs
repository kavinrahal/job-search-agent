using JobSearch.Data;
using JobSearchAgent.Agents;
using Microsoft.EntityFrameworkCore;

namespace JobSearchAgent.Tests;

public class ApplicationTrackerTests
{
    // TC01
    [Fact]
    public void NonJobRelatedEmail_ProcessClassifications_ReturnsZeroCounters()
    {
        var db = Db.Fresh();
        var email = Make.Email();
        var clf = Make.Classification(isJobRelated: false, company: "Acme", category: "application_confirmation");

        var result = ApplicationTracker.ProcessClassifications(db, [Fixtures.Pair(email, clf)]);

        Assert.Equal(0, result.Created);
        Assert.Equal(0, result.Updated);
        Assert.Equal(0, result.NotificationsQueued);
        Assert.Empty(db.Applications.ToList());
    }

    // TC02
    [Fact]
    public void RecruiterOutreachEmail_ProcessClassifications_SkipsEvenWhenJobRelated()
    {
        var db = Db.Fresh();
        var email = Make.Email();
        var clf = Make.Classification(isJobRelated: true, category: "recruiter_outreach", company: "Acme");

        var result = ApplicationTracker.ProcessClassifications(db, [Fixtures.Pair(email, clf)]);

        Assert.Equal(0, result.Created);
        Assert.Empty(db.Applications.ToList());
    }

    // TC03
    [Fact]
    public void WhitespaceCompany_ProcessClassifications_SkipsWithNoDbWrite()
    {
        var db = Db.Fresh();
        var email = Make.Email();
        var clf = Make.Classification(isJobRelated: true, category: "application_confirmation", company: "   ");

        ApplicationTracker.ProcessClassifications(db, [Fixtures.Pair(email, clf)]);

        Assert.Empty(db.Applications.ToList());
    }

    // TC04
    // The tracker creates the app at Applied, then immediately resolves application_confirmation
    // which advances Applied→Acknowledged, writing a second StatusChanged event.
    // The creation event has FromStatus=null, ToStatus=Applied.
    [Fact]
    public void ApplicationConfirmation_NoExistingApp_CreatesWithStatusApplied()
    {
        var db = Db.Fresh();
        var email = Make.Email();
        var clf = Make.Classification(category: "application_confirmation", company: "Acme", roleTitle: "Engineer");

        ApplicationTracker.ProcessClassifications(db, [Fixtures.Pair(email, clf)]);

        var app = db.Applications.First();
        var events = db.ApplicationEvents.Where(e => e.ApplicationId == app.Id).OrderBy(e => e.Id).ToList();

        // First event is the creation event: null → Applied
        var creationEvent = events.First();
        Assert.Null(creationEvent.FromStatus);
        Assert.Equal(ApplicationStatus.Applied, creationEvent.ToStatus);
    }

    // TC05
    [Fact]
    public void FollowUpNeeded_NoExistingApp_CreatesNoApplicationAndQueuesNoNotification()
    {
        var db = Db.Fresh();
        var email = Make.Email();
        var clf = Make.Classification(category: "follow_up_needed");

        var result = ApplicationTracker.ProcessClassifications(db, [Fixtures.Pair(email, clf)]);

        Assert.Equal(0, result.Created);
        Assert.Equal(0, result.NotificationsQueued);
        Assert.Empty(db.Applications.ToList());
    }

    // TC06
    [Fact]
    public void CaseInsensitiveCompany_FindOrCreate_MatchesExistingApplication()
    {
        var db = Db.Fresh();
        Seed.Application(db, company: "ACME", roleTitle: "Engineer", status: ApplicationStatus.Applied);

        var email = Make.Email();
        var clf = Make.Classification(category: "interview_invitation", company: "acme", roleTitle: "Engineer");

        ApplicationTracker.ProcessClassifications(db, [Fixtures.Pair(email, clf)]);

        Assert.Equal(1, db.Applications.Count());
        Assert.Equal("ACME", db.Applications.First().Company); // original entity preserved, not recreated with different casing
    }

    // TC07
    [Fact]
    public void BlankRoleTitle_FindOrCreate_MatchesExistingAppWithAnyRole()
    {
        var db = Db.Fresh();
        Seed.Application(db, company: "Acme", roleTitle: "Senior Engineer", status: ApplicationStatus.Applied);

        var email = Make.Email();
        var clf = Make.Classification(category: "interview_invitation", company: "Acme", roleTitle: "");

        ApplicationTracker.ProcessClassifications(db, [Fixtures.Pair(email, clf)]);

        Assert.Equal(1, db.Applications.Count());
        var app = db.Applications.First();
        Assert.Equal(ApplicationStatus.Interviewing, app.Status);
    }

    // TC08
    [Fact]
    public void TerminalStatus_Rejection_NoStatusChangeAndEmailReceivedEventWritten()
    {
        var db = Db.Fresh();
        Seed.Application(db, company: "Acme", status: ApplicationStatus.Rejected);

        var email = Make.Email();
        var clf = Make.Classification(category: "rejection", company: "Acme");

        ApplicationTracker.ProcessClassifications(db, [Fixtures.Pair(email, clf)]);

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
    public void LowerRankTransition_CanAdvanceTo_BlocksRegression()
    {
        var db = Db.Fresh();
        Seed.Application(db, company: "Acme", status: ApplicationStatus.Interviewing);

        var email = Make.Email();
        var clf = Make.Classification(category: "application_confirmation", company: "Acme");

        ApplicationTracker.ProcessClassifications(db, [Fixtures.Pair(email, clf)]);

        var app = db.Applications.First();
        Assert.Equal(ApplicationStatus.Interviewing, app.Status);
    }

    // TC10
    [Fact]
    public void StatusAdvance_ExistingApp_IncrementsUpdatedNotCreated()
    {
        var db = Db.Fresh();
        Seed.Application(db, company: "Acme", roleTitle: "Engineer", status: ApplicationStatus.Applied);

        var email = Make.Email();
        var clf = Make.Classification(category: "interview_invitation", company: "Acme", roleTitle: "Engineer");

        var result = ApplicationTracker.ProcessClassifications(db, [Fixtures.Pair(email, clf)]);

        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Updated);
    }

    // TC11
    [Fact]
    public void NewAppWithAdvancingCategory_DoesNotIncrementUpdated()
    {
        var db = Db.Fresh();
        var email = Make.Email();
        var clf = Make.Classification(category: "interview_invitation");

        var result = ApplicationTracker.ProcessClassifications(db, [Fixtures.Pair(email, clf)]);

        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Updated);
    }

    // TC12
    [Fact]
    public void SubjectOver100Chars_BuildMessage_TruncatesToExactly100()
    {
        var db = Db.Fresh();
        var email = Make.Email(subject: new string('a', 150));
        var clf = Make.Classification(category: "interview_invitation", company: "Acme");

        ApplicationTracker.ProcessClassifications(db, [Fixtures.Pair(email, clf)]);

        var notification = db.Notifications.First();
        var subjectLine = notification.Message.Split('\n').Last();
        Assert.Equal(100, subjectLine.Length);
    }

    // TC13
    [Fact]
    public void ApplicationConfirmation_ExistingAppNotAtApplied_ProducesEmailReceivedNotStatusChange()
    {
        var db = Db.Fresh();
        Seed.Application(db, company: "Acme", status: ApplicationStatus.Interviewing);

        var email = Make.Email();
        var clf = Make.Classification(category: "application_confirmation", company: "Acme");

        ApplicationTracker.ProcessClassifications(db, [Fixtures.Pair(email, clf)]);

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
    public void SchedulingRequest_AppAtScreening_NoStatusChange()
    {
        var db = Db.Fresh();
        Seed.Application(db, company: "Acme", status: ApplicationStatus.Screening);

        var email = Make.Email();
        var clf = Make.Classification(category: "scheduling_request", company: "Acme");

        ApplicationTracker.ProcessClassifications(db, [Fixtures.Pair(email, clf)]);

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
    public void InterviewInvitation_NoExistingApp_QueuesNotification()
    {
        var db = Db.Fresh();
        var email = Make.Email();
        var clf = Make.Classification(category: "interview_invitation");

        var result = ApplicationTracker.ProcessClassifications(db, [Fixtures.Pair(email, clf)]);

        Assert.Equal(1, result.NotificationsQueued);
    }

    // TC16
    [Fact]
    public void ApplicationConfirmation_NoExistingApp_DoesNotQueueNotification()
    {
        var db = Db.Fresh();
        var email = Make.Email();
        var clf = Make.Classification(category: "application_confirmation");

        var result = ApplicationTracker.ProcessClassifications(db, [Fixtures.Pair(email, clf)]);

        Assert.Equal(0, result.NotificationsQueued);
    }

    // TC17
    // application_confirmation on a new app writes exactly two StatusChanged events:
    // (null→Applied) from FindOrCreate, then (Applied→Acknowledged) from ProcessClassifications.
    [Fact]
    public void NewApp_ApplicationConfirmation_WritesTwoStatusChangedEvents()
    {
        var db = Db.Fresh();
        var email = Make.Email();
        var clf = Make.Classification(category: "application_confirmation");

        ApplicationTracker.ProcessClassifications(db, [Fixtures.Pair(email, clf)]);

        var app = db.Applications.First();
        var events = db.ApplicationEvents.Where(e => e.ApplicationId == app.Id).OrderBy(e => e.Id).ToList();
        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.Equal(ApplicationEventType.StatusChanged, e.EventType));
    }

    // TC18
    [Fact]
    public void OfferOnTerminalRejectedApp_StatusUnchangedAndNotificationStillQueued()
    {
        var db = Db.Fresh();
        Seed.Application(db, company: "Acme", status: ApplicationStatus.Rejected);

        var email = Make.Email();
        var clf = Make.Classification(category: "offer", company: "Acme");

        var result = ApplicationTracker.ProcessClassifications(db, [Fixtures.Pair(email, clf)]);

        var app = db.Applications.First();
        Assert.Equal(ApplicationStatus.Rejected, app.Status);
        Assert.Equal(1, result.NotificationsQueued);
    }
}
