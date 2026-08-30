using JobSearch.Data;

namespace AdminDashboard.Api.Services;

// Writes the one AdminAuditLog row every Emergency action produces. Always goes through the
// write-configured AppDbContext instance passed in, same as the action's own database change —
// see the Emergency page model for why (actions 1-4 use the write connection so they keep
// working even if JobSearch.Api itself is down).
public static class AdminAuditService
{
    // "owner" rather than a real admin identity — single-admin tool, see AdminAuditLog's own
    // doc comment for why there's nothing more specific to record.
    private const string Actor = "owner";

    public static async Task LogAsync(AppDbContext writeDb, string action, int? targetUserId, string detail)
    {
        writeDb.AdminAuditLogs.Add(new AdminAuditLog
        {
            Action = action,
            TargetUserId = targetUserId,
            Detail = $"[{Actor}] {detail}",
            PerformedAt = DateTime.UtcNow,
        });
        await writeDb.SaveChangesAsync();
    }
}
