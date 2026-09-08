using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Api.Tests;

public class WorkerLockServiceTests
{
    private static readonly DateTime Now = new(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);

    private static AppDbContext FreshDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // TC01 — First acquire against an empty table succeeds.
    [Fact]
    public async Task TryAcquireAsync_NoExistingRow_Succeeds()
    {
        using var db = FreshDb();

        Assert.True(await WorkerLockService.TryAcquireAsync(db, Now));
    }

    // TC02 — A second acquire while the lock is still fresh fails.
    // This is the actual overlap guard: without it, a second cron trigger firing while the
    // first run is still in progress would silently start processing the same users twice.
    [Fact]
    public async Task TryAcquireAsync_AlreadyHeldAndFresh_Fails()
    {
        using var db = FreshDb();
        await WorkerLockService.TryAcquireAsync(db, Now);

        var second = await WorkerLockService.TryAcquireAsync(db, Now.AddMinutes(5));

        Assert.False(second);
    }

    // TC03 — A lock held past the stale window can be re-acquired (crash recovery).
    // Silent failure: without this, a process that died without releasing would block every
    // future run forever, with nothing to ever clear it.
    [Fact]
    public async Task TryAcquireAsync_HeldPastStaleWindow_Succeeds()
    {
        using var db = FreshDb();
        await WorkerLockService.TryAcquireAsync(db, Now);

        var reacquired = await WorkerLockService.TryAcquireAsync(db, Now.AddHours(3));

        Assert.True(reacquired);
    }

    // TC03b — The actual regression test for the bug being fixed. Two independent contexts
    // both load the free lock *before* either commits a claim (the real race window an
    // overlapping cron trigger would hit), then db1 claims it first. db2's tracked copy is
    // now stale relative to the store, so its own claim attempt must fail rather than
    // silently overwrite db1's.
    //
    // This is staged explicitly (load, load, claim, claim) rather than via Task.WhenAll:
    // EF Core's InMemory provider resolves every await synchronously, so two "concurrent"
    // TryAcquireAsync calls started via Task.WhenAll never actually interleave — they just
    // run to completion sequentially, which would pass even against the old, unguarded
    // load-then-save code (confirmed while writing this test: CreditServiceTests'
    // Task.WhenAll-based concurrent test has this same property). Explicit staging is the
    // one shape that reliably forces the race on this provider, which is what "most
    // rigorous approximation this repo's test conventions support" comes down to here.
    //
    // Silent failure without the fix: a plain load-then-save check has no way to detect
    // db2's copy went stale — it would unconditionally overwrite db1's claim and return
    // true for both, letting two worker runs process the same users at once.
    [Fact]
    public async Task TryAcquireAsync_SecondClaimOnStaleLoad_FailsInsteadOfOverwriting()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using (var seedDb = new AppDbContext(options))
        {
            Assert.True(await WorkerLockService.TryAcquireAsync(seedDb, Now));
            await WorkerLockService.ReleaseAsync(seedDb);
        }

        await using var db1 = new AppDbContext(options);
        await using var db2 = new AppDbContext(options);

        // Both contexts load the free row into their own change tracker before either
        // claims — this is what makes db2's later claim operate on stale data.
        await db1.WorkerLocks.FirstOrDefaultAsync();
        await db2.WorkerLocks.FirstOrDefaultAsync();

        Assert.True(await WorkerLockService.TryAcquireAsync(db1, Now.AddMinutes(10)));
        Assert.False(await WorkerLockService.TryAcquireAsync(db2, Now.AddMinutes(10)));
    }

    // TC04 — Releasing lets the next acquire succeed immediately, not just after the stale window.
    [Fact]
    public async Task ReleaseAsync_ThenAcquire_SucceedsImmediately()
    {
        using var db = FreshDb();
        await WorkerLockService.TryAcquireAsync(db, Now);

        await WorkerLockService.ReleaseAsync(db);

        Assert.True(await WorkerLockService.TryAcquireAsync(db, Now.AddSeconds(1)));
    }

    // TC05 — Releasing before anything was ever acquired is a no-op, not an exception.
    [Fact]
    public async Task ReleaseAsync_NoExistingRow_DoesNotThrow()
    {
        using var db = FreshDb();

        var exception = await Record.ExceptionAsync(() => WorkerLockService.ReleaseAsync(db));

        Assert.Null(exception);
    }
}
