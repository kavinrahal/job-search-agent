namespace JobSearch.Data;

// Single-row table (see the migration's seed) driving the main app's maintenance-mode
// banner/full-page notice — JobSearch.Api's unauthenticated GET /api/v1/status reads it, and
// AdminDashboard.Api's Emergency page writes it. Always exactly one row: every read/write
// should use SingleAsync()/FirstAsync(), never a list query.
public class SiteStatus
{
    public int Id { get; set; }
    public bool MaintenanceMode { get; set; }
    public string? MaintenanceMessage { get; set; }
    public bool BannerActive { get; set; }
    public string? BannerMessage { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
