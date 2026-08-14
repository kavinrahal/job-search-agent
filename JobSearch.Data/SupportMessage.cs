namespace JobSearch.Data;

// No query filter — the whole point is the owner viewing everyone's submissions via the
// admin endpoint, same reasoning as AnalyticsEvent.
public class SupportMessage
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Email { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
