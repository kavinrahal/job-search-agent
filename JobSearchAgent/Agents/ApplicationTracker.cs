using JobSearch.Data;
using JobSearchAgent.Models;
using Microsoft.EntityFrameworkCore;

namespace JobSearchAgent.Agents;

public record TrackingResult(int Created, int Updated, int NotificationsQueued);

public static class ApplicationTracker
{
    // Forward-only status ordering — terminal statuses share the highest rank.
    private static readonly Dictionary<string, int> StatusRank = new()
    {
        [ApplicationStatus.Applied]      = 0,
        [ApplicationStatus.Acknowledged] = 1,
        [ApplicationStatus.Screening]    = 2,
        [ApplicationStatus.Interviewing] = 3,
        [ApplicationStatus.FinalRound]   = 4,
        [ApplicationStatus.Offer]        = 5,
        [ApplicationStatus.Rejected]     = 5,
        [ApplicationStatus.Ghosted]      = 5,
        [ApplicationStatus.Withdrawn]    = 5,
    };

    private static bool IsTerminal(string status) =>
        status is ApplicationStatus.Offer or ApplicationStatus.Rejected
            or ApplicationStatus.Ghosted or ApplicationStatus.Withdrawn;

    private static bool CanAdvanceTo(string current, string target)
    {
        if (IsTerminal(current)) return false;
        StatusRank.TryGetValue(current, out int cr);
        StatusRank.TryGetValue(target, out int tr);
        return tr > cr;
    }

    public static async Task<TrackingResult> ProcessClassificationsAsync(
        AppDbContext db,
        IEnumerable<(RawEmail Email, EmailClassification Classification)> results)
    {
        var now = DateTime.UtcNow;
        int created = 0, updated = 0, notifications = 0;

        foreach (var (email, clf) in results.Where(r => r.Classification.IsJobRelated))
        {
            // Skip categories that don't correspond to submitted applications
            if (clf.Category is "not_relevant" or "recruiter_outreach")
                continue;

            // Need at minimum a company name to track
            if (string.IsNullOrWhiteSpace(clf.Company))
                continue;

            var (app, wasCreated) = await FindOrCreateAsync(db, clf, email, now);
            if (app is null) continue;

            if (wasCreated)
                created++;

            var (toStatus, summary) = ResolveTransition(app.Status, clf);

            // Only advance status if the application isn't in a terminal state
            bool statusChanged = toStatus != app.Status && !IsTerminal(app.Status);
            if (statusChanged)
            {
                var ev = new ApplicationEvent
                {
                    ApplicationId = app.Id,
                    EventType = ApplicationEventType.StatusChanged,
                    FromStatus = app.Status,
                    ToStatus = toStatus,
                    MessageId = email.MessageId,
                    Summary = summary,
                    OccurredAt = email.ReceivedAt.UtcDateTime,
                };
                db.ApplicationEvents.Add(ev);
                app.Status = toStatus;
                app.UpdatedAt = now;
                if (!wasCreated) updated++;
            }
            else if (!wasCreated)
            {
                // Log the email even if status didn't change
                db.ApplicationEvents.Add(new ApplicationEvent
                {
                    ApplicationId = app.Id,
                    EventType = ApplicationEventType.EmailReceived,
                    MessageId = email.MessageId,
                    Summary = summary,
                    OccurredAt = email.ReceivedAt.UtcDateTime,
                });
            }

            // Queue a Telegram notification for high-priority categories
            var notifType = GetNotificationType(clf.Category);
            if (notifType is not null)
            {
                db.Notifications.Add(new Notification
                {
                    Type = notifType,
                    Message = BuildMessage(clf, email),
                    ApplicationId = app.Id,
                    CreatedAt = now,
                });
                notifications++;
            }
        }

        await db.SaveChangesAsync();
        return new TrackingResult(created, updated, notifications);
    }

    private static async Task<(Application? app, bool wasCreated)> FindOrCreateAsync(
        AppDbContext db,
        EmailClassification clf,
        RawEmail email,
        DateTime now)
    {
        string company = clf.Company.Trim();
        string role = clf.RoleTitle.Trim();

        // EF Core translates string.Equals with StringComparison.OrdinalIgnoreCase to ILIKE on PostgreSQL
        var existing = await db.Applications.FirstOrDefaultAsync(a =>
            string.Equals(a.Company, company, StringComparison.OrdinalIgnoreCase) &&
            (role == "" || string.Equals(a.RoleTitle, role, StringComparison.OrdinalIgnoreCase)));

        if (existing is not null)
            return (existing, false);

        // Only create a new application for categories that imply one was submitted
        if (clf.Category is not (
            "application_confirmation" or "rejection" or
            "interview_invitation" or "scheduling_request" or "offer"))
            return (null, false);

        var app = new Application
        {
            Company = company,
            RoleTitle = role,
            Status = ApplicationStatus.Applied,
            AppliedAt = email.ReceivedAt.UtcDateTime,
            UpdatedAt = now,
        };
        db.Applications.Add(app);
        await db.SaveChangesAsync(); // flush to get app.Id before adding events

        db.ApplicationEvents.Add(new ApplicationEvent
        {
            ApplicationId = app.Id,
            EventType = ApplicationEventType.StatusChanged,
            FromStatus = null,
            ToStatus = ApplicationStatus.Applied,
            MessageId = email.MessageId,
            Summary = $"Application tracked: {company}{(role.Length > 0 ? $" — {role}" : "")}",
            OccurredAt = email.ReceivedAt.UtcDateTime,
        });

        return (app, true);
    }

    private static (string newStatus, string summary) ResolveTransition(
        string current, EmailClassification clf)
    {
        string co = clf.Company;
        string ro = clf.RoleTitle.Length > 0 ? $" — {clf.RoleTitle}" : "";

        return clf.Category switch
        {
            "application_confirmation" when current == ApplicationStatus.Applied
                => (ApplicationStatus.Acknowledged, $"Application acknowledged by {co}"),

            "interview_invitation" when CanAdvanceTo(current, ApplicationStatus.Interviewing)
                => (ApplicationStatus.Interviewing, $"Interview invitation from {co}{ro}"),

            "scheduling_request" when CanAdvanceTo(current, ApplicationStatus.Screening)
                => (ApplicationStatus.Screening, $"Interview scheduling in progress at {co}"),

            "offer"     => (ApplicationStatus.Offer,    $"Offer received from {co}{ro}"),
            "rejection" => (ApplicationStatus.Rejected, $"Rejected by {co}{ro}"),

            _ => (current, $"Email received from {co}{ro}"),
        };
    }

    private static string? GetNotificationType(string category) => category switch
    {
        "interview_invitation" => NotificationType.InterviewInvite,
        "offer"                => NotificationType.Offer,
        "follow_up_needed"     => NotificationType.ActionNeeded,
        "rejection"            => NotificationType.Rejection,
        _                      => null,
    };

    private static string BuildMessage(EmailClassification clf, RawEmail email)
    {
        string ro = clf.RoleTitle.Length > 0 ? $" — {clf.RoleTitle}" : "";
        string subj = email.Subject.Length > 100 ? email.Subject[..100] : email.Subject;
        return clf.Category switch
        {
            "interview_invitation" => $"Interview invite: {clf.Company}{ro}\n{subj}",
            "offer"                => $"Offer: {clf.Company}{ro}\n{subj}",
            "follow_up_needed"     => $"Action needed: {clf.Company}{ro}\n{subj}",
            "rejection"            => $"Rejection: {clf.Company}{ro}\n{subj}",
            _                      => $"{clf.Company}{ro}: {subj}",
        };
    }
}
