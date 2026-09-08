using Microsoft.EntityFrameworkCore;

namespace JobSearch.Data;

// Single-row lock — there's exactly one worker process today, so one row is enough.
public class WorkerLock
{
    public int Id { get; set; }
    public DateTime? AcquiredAt { get; set; }

    // Optimistic concurrency token guarding the claim in TryAcquireAsync below — same
    // pattern as User.CreditVersion / CreditService's guard.
    public int LockVersion { get; set; }
}

// A cron-triggered worker run can take a while; if the next trigger fires before the
// previous run has finished, this stops the two runs from processing the same users at
// once. Guarded by WorkerLock.LockVersion, an optimistic concurrency token — the same
// pattern CreditService uses for User.CreditVersion. Two overlapping claims can both load
// the row and both see it as free/stale, but only one SaveChangesAsync can win: whichever
// lands first bumps LockVersion, and the second's save then fails with
// DbUpdateConcurrencyException instead of silently overwriting the first claim. That makes
// the claim itself atomic (backed by Postgres's row-level MVCC check on UPDATE), rather
// than merely safe by convention because cron triggers happen to fire minutes-to-hours
// apart — this now also holds if a second trigger fires seconds later, or eventually if
// there's more than one worker replica.
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
        existing.LockVersion += 1;
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another claim's SaveChangesAsync landed first and moved LockVersion out from
            // under us — we lost the race, not an error.
            return false;
        }
    }

    public static async Task ReleaseAsync(AppDbContext db)
    {
        var existing = await db.WorkerLocks.FirstOrDefaultAsync();
        if (existing is null) return;
        existing.AcquiredAt = null;
        await db.SaveChangesAsync();
    }
}
