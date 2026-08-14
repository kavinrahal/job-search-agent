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
