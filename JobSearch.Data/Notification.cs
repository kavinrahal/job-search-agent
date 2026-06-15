namespace JobSearch.Data;

public class Notification
{
    public int Id { get; set; }
    public string Type { get; set; } = "";        // see NotificationType
    public string Message { get; set; } = "";
    public int? ApplicationId { get; set; }
    public DateTime? SentAt { get; set; }         // null = pending
    public DateTime CreatedAt { get; set; }

    public Application? Application { get; set; }
}

public static class NotificationType
{
    public const string InterviewInvite = "InterviewInvite";
    public const string Offer           = "Offer";
    public const string ActionNeeded    = "ActionNeeded";
    public const string Rejection       = "Rejection";
    public const string DailyDigest     = "DailyDigest";
}
