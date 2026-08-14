using Microsoft.EntityFrameworkCore;

namespace JobSearch.Data;

// Single-row lock — there's exactly one worker process today, so one row is enough.
public class WorkerLock
{
    public int Id { get; set; }
    public DateTime? AcquiredAt { get; set; }
}

// A cron-triggered worker run can take a while; if the next trigger fires before the
// previous run has finished, this stops the two runs from processing the same users at
// once. Not built for millisecond-scale races (unlike CreditService's concurrency guard) —
// cron triggers are minutes to hours apart, so a plain load-then-save check is enough; by
// the time a second run's TryAcquireAsync executes, the first run's acquire has long since
// committed.
public static class WorkerLockService
{
    // ponytail: no try/finally release on crash — if the process dies mid-run the lock
    // just sits held until this expires, and the run after that recovers on its own. A
    // guaranteed-release path isn't worth the complexity for a failure mode that already
    // self-heals within one stale window.
    private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(2);

    public static async Task<bool> TryAcquireAsync(AppDbContext db, DateTime now)
    {
        var existing = await db.WorkerLocks.FirstOrDefaultAsync();
        if (existing is null)
        {
            db.WorkerLocks.Add(new WorkerLock { AcquiredAt = now });
            await db.SaveChangesAsync();
            return true;
        }

        if (existing.AcquiredAt is DateTime acquiredAt && now - acquiredAt < StaleAfter)
            return false;

        existing.AcquiredAt = now;
        await db.SaveChangesAsync();
        return true;
    }

    public static async Task ReleaseAsync(AppDbContext db)
    {
        var existing = await db.WorkerLocks.FirstOrDefaultAsync();
        if (existing is null) return;
        existing.AcquiredAt = null;
        await db.SaveChangesAsync();
    }
}
