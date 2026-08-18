namespace JobSearch.Data;

public class DiscoveredPosting
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Url { get; set; } = "";
    public string Source { get; set; } = "";       // "seek"
    public string Title { get; set; } = "";
    public string Company { get; set; } = "";
    public string? Recommendation { get; set; }    // null until evaluated; "error" on fetch/eval failure
    public string? EvaluationJson { get; set; }    // full serialised PostingEvaluation
    public string? DisqualifierHit { get; set; }
    public DateTime DiscoveredAt { get; set; }
    public DateTime? EvaluatedAt { get; set; }
    public bool NotificationSent { get; set; }             // Telegram
    public bool EmailNotificationSent { get; set; }
}
