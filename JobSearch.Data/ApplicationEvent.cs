namespace JobSearch.Data;

public class ApplicationEvent
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ApplicationId { get; set; }
    public string EventType { get; set; } = "";   // see ApplicationEventType
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public string? MessageId { get; set; }        // soft ref to RawEmails.MessageId
    public string Summary { get; set; } = "";
    public DateTime OccurredAt { get; set; }

    public Application Application { get; set; } = null!;
}

public static class ApplicationEventType
{
    public const string EmailReceived  = "EmailReceived";
    public const string StatusChanged  = "StatusChanged";
    public const string ManualUpdate   = "ManualUpdate";
}
