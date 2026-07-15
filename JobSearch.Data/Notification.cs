namespace JobSearch.Data;

public class Notification
{
    public int Id { get; set; }
    public string Type { get; set; } = "";        // see NotificationType
    public string Message { get; set; } = "";
    public int? ApplicationId { get; set; }
    public DateTime? SentAt { get; set; }         // null = pending (Telegram)
    public DateTime? WhatsAppSentAt { get; set; } // null = pending (WhatsApp) — independent of SentAt
    public string? WhatsAppMessageId { get; set; } // wamid of the sent teaser, for reply-threading
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
