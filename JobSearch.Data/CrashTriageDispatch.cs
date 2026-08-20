namespace JobSearch.Data;

// One row per Sentry issue the fix agent was woken for. Serves two purposes at once:
// dedup (never dispatch the same issue twice, since webhook delivery retries) and the
// global hourly rate limit (count rows in the last hour).
//
// Not user-scoped — this is operational data about the app itself, not tenant data, so it
// has no UserId and no query filter.
public class CrashTriageDispatch
{
    public int Id { get; set; }

    // Sentry's own issue id. Unique — the dedup guarantee is enforced by the database, not
    // just by the check in CrashTriage, so a concurrent double-delivery still can't produce
    // two agent runs.
    public string SentryIssueId { get; set; } = "";

    public string Title { get; set; } = "";
    public string ProjectSlug { get; set; } = "";
    public DateTime DispatchedAt { get; set; }
}
