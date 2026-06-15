namespace JobSearch.Data;

public class RawEmailRecord
{
    public int Id { get; set; }
    public string MessageId { get; set; } = "";
    public string ThreadId { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string Subject { get; set; } = "";
    public string BodyText { get; set; } = "";
    public DateTime ReceivedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
