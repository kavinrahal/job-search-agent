using JobSearch.Data;
using JobSearchAgent.Agents;
using JobSearchAgent.Models;

namespace JobSearchAgent.Storage;

public static class EmailRepository
{
    public static bool UpsertRawEmail(AppDbContext db, RawEmail email)
    {
        if (db.RawEmails.Any(r => r.MessageId == email.MessageId))
            return false;

        db.RawEmails.Add(new RawEmailRecord
        {
            MessageId = email.MessageId,
            ThreadId = email.ThreadId,
            FromAddress = email.FromAddress,
            Subject = email.Subject,
            BodyText = email.BodyText,
            ReceivedAt = email.ReceivedAt.UtcDateTime,
        });
        db.SaveChanges();
        return true;
    }

    public static DateTimeOffset? GetLatestReceivedAt(AppDbContext db)
    {
        DateTime? latest = db.RawEmails
            .OrderByDescending(r => r.ReceivedAt)
            .Select(r => (DateTime?)r.ReceivedAt)
            .FirstOrDefault();

        return latest.HasValue ? new DateTimeOffset(latest.Value, TimeSpan.Zero) : null;
    }

    public static List<RawEmail> GetUnclassified(AppDbContext db)
    {
        return db.RawEmails
            .Where(r => !db.Classifications.Any(c => c.MessageId == r.MessageId))
            .AsEnumerable()
            .Select(r => new RawEmail(
                r.MessageId, r.ThreadId, r.FromAddress, r.Subject, r.BodyText,
                new DateTimeOffset(r.ReceivedAt, TimeSpan.Zero)))
            .ToList();
    }

    public static void SaveClassifications(
        AppDbContext db,
        IEnumerable<(string MessageId, EmailClassification Classification)> results)
    {
        var now = DateTime.UtcNow;
        foreach (var (messageId, clf) in results)
        {
            var existing = db.Classifications.FirstOrDefault(c => c.MessageId == messageId);
            if (existing is not null)
            {
                existing.IsJobRelated = clf.IsJobRelated;
                existing.Category = clf.Category;
                existing.Confidence = clf.Confidence;
                existing.Company = clf.Company;
                existing.RoleTitle = clf.RoleTitle;
                existing.ClassifiedAt = now;
            }
            else
            {
                db.Classifications.Add(new ClassificationRecord
                {
                    MessageId = messageId,
                    IsJobRelated = clf.IsJobRelated,
                    Category = clf.Category,
                    Confidence = clf.Confidence,
                    Company = clf.Company,
                    RoleTitle = clf.RoleTitle,
                    ClassifiedAt = now,
                });
            }
        }
        db.SaveChanges();
    }
}
