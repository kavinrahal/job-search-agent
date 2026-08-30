namespace JobSearch.Data;

// One row per break-glass action taken from AdminDashboard.Api's Emergency page. Single-admin
// tool — there's no admin identity to record beyond "owner" (see AdminAuditActions.Actor) — the
// point is a durable trail of what changed and when, not who among several admins did it.
public class AdminAuditLog
{
    public int Id { get; set; }
    public string Action { get; set; } = "";       // see AdminAuditActions
    public int? TargetUserId { get; set; }
    public string Detail { get; set; } = "";        // human-readable before -> after, e.g. "creditBalance: 4 -> 9"
    public DateTime PerformedAt { get; set; }
}

// The fixed set of Action values AdminDashboard.Api's Emergency page can write. Kept here
// (JobSearch.Data) rather than in AdminDashboard.Api so any future reader of this table --
// on either side -- has one canonical list to check against instead of matching magic strings.
public static class AdminAuditActions
{
    public const string CreditAdjust = "credit_adjust";
    public const string TierChange = "tier_change";
    public const string Deactivate = "deactivate";
    public const string WorkerLockCleared = "worker_lock_cleared";
    public const string MaintenanceModeToggled = "maintenance_mode_toggled";
    public const string BannerUpdated = "banner_updated";
}
