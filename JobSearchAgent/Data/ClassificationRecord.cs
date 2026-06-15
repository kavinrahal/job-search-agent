namespace JobSearchAgent.Data;

public class ClassificationRecord
{
    public int Id { get; set; }
    public string MessageId { get; set; } = "";
    public bool IsJobRelated { get; set; }
    public string Category { get; set; } = "";
    public double Confidence { get; set; }
    public string Company { get; set; } = "";
    public string RoleTitle { get; set; } = "";
    public DateTime ClassifiedAt { get; set; }
}
