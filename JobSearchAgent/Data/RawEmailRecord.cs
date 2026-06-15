namespace JobSearchAgent.Data;

public class RawEmailRecord
{
    public int Id { get; set; }
    public string MessageId { get; set; } = "";
    public string ThreadId { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string Subject { get; set; } = "";
    public string BodyText { get; set; } = "";
    public DateTime ReceivedAt { get; set; }       // stored as UTC
    public DateTime? ProcessedAt { get; set; }     // stored as UTC
}
