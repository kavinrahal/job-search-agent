namespace JobSearch.Data;

public class Application
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Company { get; set; } = "";
    public string RoleTitle { get; set; } = "";
    public string? JobUrl { get; set; }
    public string Status { get; set; } = ApplicationStatus.Applied;
    public DateTime AppliedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? Notes { get; set; }

    // Only set for filter-tracking-mode applications (GmailSettingsClient.EnsureCompanyFilterAsync
    // installs a Gmail filter forwarding this domain's mail) — also used as a secondary,
    // more reliable match key in ApplicationTracker.FindOrCreateAsync, since company-name
    // matching alone misses realistic LLM classification variance ("Acme Corp" vs "Acme Corporation").
    public string? CompanyDomain { get; set; }

    public List<ApplicationEvent> Events { get; set; } = [];
}

// Status values for Application.Status — stored as strings so the DB is readable.
public static class ApplicationStatus
{
    public const string Applied       = "Applied";
    public const string Acknowledged  = "Acknowledged";
    public const string Screening     = "Screening";
    public const string Interviewing  = "Interviewing";
    public const string FinalRound    = "FinalRound";
    public const string Offer         = "Offer";
    public const string Rejected      = "Rejected";
    public const string Ghosted       = "Ghosted";
    public const string Withdrawn     = "Withdrawn";

    // Shared across the GET filter param and PATCH validation — previously duplicated inline.
    public static readonly HashSet<string> All =
    [
        Applied, Acknowledged, Screening, Interviewing,
        FinalRound, Offer, Rejected, Ghosted, Withdrawn,
    ];
}
