namespace JobSearch.Data;

public class SystemHealth
{
    public int Id { get; set; }
    public DateTime CheckedAt { get; set; }
    public int EmailsFetched { get; set; }
    public int EmailsClassified { get; set; }
    public int NewApplications { get; set; }
    public int DurationMs { get; set; }
    public string? Error { get; set; }
}
