using JobSearch.Data;
using JobSearchAgent.Models;
using Microsoft.EntityFrameworkCore;

namespace JobSearchAgent.Agents;

public record TrackingResult(int Created, int Updated);

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
        int created = 0, updated = 0;

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
                    UserId = db.CurrentUserId!.Value,
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
                    UserId = db.CurrentUserId!.Value,
                    ApplicationId = app.Id,
                    EventType = ApplicationEventType.EmailReceived,
                    MessageId = email.MessageId,
                    Summary = summary,
                    OccurredAt = email.ReceivedAt.UtcDateTime,
                });
            }
        }

        await db.SaveChangesAsync();
        return new TrackingResult(created, updated);
    }

    private static async Task<(Application? app, bool wasCreated)> FindOrCreateAsync(
        AppDbContext db,
        EmailClassification clf,
        RawEmail email,
        DateTime now)
    {
        string company = clf.Company.Trim();
        string role = clf.RoleTitle.Trim();

        // Secondary match key: a filter-tracking-mode application logged manually carries the
        // company's email domain (see POST /applications), which is far more reliable than
        // company-name matching for catching the same real-world application under realistic
        // LLM classification variance ("Acme Corp" vs "Acme Corporation" won't match on name
        // alone, but both emails come from the same domain).
        var fromDomain = ExtractDomain(email.FromAddress);

        // Case-insensitive match — EF Core translates ToLower() to LOWER() in PostgreSQL.
        // string.Equals(..., StringComparison.OrdinalIgnoreCase) looks equivalent and passes
        // against the test suite's InMemory provider, but Npgsql cannot translate that overload
        // to SQL at all and throws InvalidOperationException at runtime against real Postgres.
#pragma warning disable RCS1155 // ToLower() is required here for SQL translation, not in-memory comparison
        var existing = await db.Applications.FirstOrDefaultAsync(a =>
            (a.Company.ToLower() == company.ToLower() &&
             (role == "" || a.RoleTitle.ToLower() == role.ToLower()))
            || (fromDomain != null && a.CompanyDomain == fromDomain));
#pragma warning restore RCS1155

        if (existing is not null)
            return (existing, false);

        // Only create a new application for categories that imply one was submitted
        if (clf.Category is not (
            "application_confirmation" or "rejection" or
            "interview_invitation" or "scheduling_request" or "offer"))
            return (null, false);

        var app = new Application
        {
            UserId = db.CurrentUserId!.Value,
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
            UserId = db.CurrentUserId!.Value,
            ApplicationId = app.Id,
            EventType = ApplicationEventType.StatusChanged,
            FromStatus = null,
            ToStatus = ApplicationStatus.Applied,
            MessageId = email.MessageId,
            Summary = $"Application tracked: {company}{(role.Length > 0 ? $" - {role}" : "")}",
            OccurredAt = email.ReceivedAt.UtcDateTime,
        });

        return (app, true);
    }

    private static (string newStatus, string summary) ResolveTransition(
        string current, EmailClassification clf)
    {
        string co = clf.Company;
        string ro = clf.RoleTitle.Length > 0 ? $" - {clf.RoleTitle}" : "";

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

    // Lowercased domain from a "From" header, e.g. "Name <hr@acmecorp.com>" -> "acmecorp.com".
    // MailAddress already handles the quoted-display-name-plus-angle-brackets format (and
    // a bare address with none of that) correctly, so no need to hand-parse it.
    internal static string? ExtractDomain(string fromHeader)
    {
        try { return new System.Net.Mail.MailAddress(fromHeader).Host.ToLowerInvariant(); }
        catch (FormatException) { return null; }
    }
}
