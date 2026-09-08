using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Api.Tests;

public class GmailConnectionBrokenServiceTests
{
    private static readonly DateTime Now = new(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);

    private static AppDbContext FreshDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static User NewUser(int id) => new()
    {
        Id = id,
        Email = $"user{id}@example.com",
        CreatedAt = Now,
    };

    // TC01 — The first TokenResponseException the worker hits for a user marks it broken and
    // reports "newly broken" — this is what gates sending the reconnect email.
    [Fact]
    public async Task MarkBrokenIfNewAsync_NotPreviouslyBroken_MarksAndReturnsTrue()
    {
        using var db = FreshDb();
        var user = NewUser(1);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var newlyBroken = await GmailConnectionBrokenService.MarkBrokenIfNewAsync(db, user, Now);

        Assert.True(newlyBroken);
        Assert.Equal(Now, user.GmailConnectionBrokenAt);
    }

    // TC02 — The actual anti-spam behavior: a second cron run hitting the same still-broken
    // connection must not re-mark (or, via the caller's use of this return value, re-email).
    // Distinguishes the "already known broken" case from a fresh revocation event.
    [Fact]
    public async Task MarkBrokenIfNewAsync_AlreadyBroken_ReturnsFalseAndDoesNotOverwriteTimestamp()
    {
        using var db = FreshDb();
        var firstBrokenAt = Now;
        var user = NewUser(2);
        user.GmailConnectionBrokenAt = firstBrokenAt;
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var newlyBroken = await GmailConnectionBrokenService.MarkBrokenIfNewAsync(db, user, Now.AddDays(1));

        Assert.False(newlyBroken);
        Assert.Equal(firstBrokenAt, user.GmailConnectionBrokenAt);
    }

    // TC03 — The reconnect flow (JobSearch.Api's /gmail-oauth/callback) clears the flag once a
    // fresh refresh token proves Gmail access works again.
    [Fact]
    public void ClearBroken_PreviouslyBroken_SetsNull()
    {
        var user = NewUser(3);
        user.GmailConnectionBrokenAt = Now;

        GmailConnectionBrokenService.ClearBroken(user);

        Assert.Null(user.GmailConnectionBrokenAt);
    }

    // TC04 — Calling ClearBroken on a user that was never broken is a harmless no-op, not an
    // error — the reconnect callback calls this unconditionally on every full-scope exchange.
    [Fact]
    public void ClearBroken_NeverBroken_StaysNull()
    {
        var user = NewUser(4);

        GmailConnectionBrokenService.ClearBroken(user);

        Assert.Null(user.GmailConnectionBrokenAt);
    }

    // TC05 — The worker's activeUsers query (JobSearchAgent/Program.cs) excludes anyone with
    // GmailConnectionBrokenAt set, so a known-broken connection isn't retried every cron run.
    // Exercises the same predicate that query filters on, against a mix of broken/unbroken users.
    [Fact]
    public async Task BrokenConnectionFlag_ExcludesUserFromActiveProcessing()
    {
        using var db = FreshDb();
        var healthy = NewUser(5);
        var broken = NewUser(6);
        broken.GmailConnectionBrokenAt = Now;
        db.Users.AddRange(healthy, broken);
        await db.SaveChangesAsync();

        var eligible = await db.Users.Where(u => u.GmailConnectionBrokenAt == null).Select(u => u.Id).ToListAsync();

        Assert.Contains(healthy.Id, eligible);
        Assert.DoesNotContain(broken.Id, eligible);
    }

    // TC06 — Once reconnected, the same user becomes eligible for the active-users query again
    // without any other state changing — end-to-end of the mark -> exclude -> reconnect -> clear
    // -> include-again cycle described in the task.
    [Fact]
    public async Task BrokenConnectionFlag_ClearedAfterReconnect_UserBecomesEligibleAgain()
    {
        using var db = FreshDb();
        var user = NewUser(7);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await GmailConnectionBrokenService.MarkBrokenIfNewAsync(db, user, Now);
        Assert.DoesNotContain(user.Id, await db.Users.Where(u => u.GmailConnectionBrokenAt == null).Select(u => u.Id).ToListAsync());

        GmailConnectionBrokenService.ClearBroken(user);
        await db.SaveChangesAsync();

        Assert.Contains(user.Id, await db.Users.Where(u => u.GmailConnectionBrokenAt == null).Select(u => u.Id).ToListAsync());
    }
}
