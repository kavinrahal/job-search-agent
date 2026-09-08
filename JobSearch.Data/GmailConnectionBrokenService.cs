namespace JobSearch.Data;

// Shared logic around User.GmailConnectionBrokenAt — set by the worker's per-user loop
// (JobSearchAgent/Program.cs) when a TokenResponseException proves a user's GmailRefreshToken
// has been revoked or expired, and cleared by the OAuth reconnect callback
// (JobSearch.Api/Program.cs's /gmail-oauth/callback) once a fresh token proves it's fixed. Kept
// out of both Program.cs files so the "was this newly broken, should we email?" decision is
// unit-testable without spinning up the actual Gmail/OAuth plumbing — same reasoning as
// WorkerLockService living here instead of inline in the worker.
public static class GmailConnectionBrokenService
{
    // Marks the connection broken if it wasn't already, persists it, and returns whether this
    // call is the one that just marked it. The caller uses that return value to decide whether
    // to send the one-time reconnect email — false means an earlier run already marked (and
    // presumably already emailed) this exact revocation event, so nothing more happens here.
    public static async Task<bool> MarkBrokenIfNewAsync(AppDbContext db, User user, DateTime brokenAt)
    {
        if (user.GmailConnectionBrokenAt is not null) return false;

        user.GmailConnectionBrokenAt = brokenAt;
        await db.SaveChangesAsync();
        return true;
    }

    // Clears the flag on a successful reconnect. Safe to call unconditionally on every
    // successful full-scope token exchange — a no-op if it wasn't set, so the caller doesn't
    // need to branch on prior state first.
    public static void ClearBroken(User user) => user.GmailConnectionBrokenAt = null;
}
