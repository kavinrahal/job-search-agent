using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Api.Tests;

// Regression coverage for the credit-charged-on-refresh bug: a page refresh while /cv or
// /letter is still generating loses the response, not the server-side work (see
// AgentThread.ClientRequestId's own comment — nothing threads a CancellationToken through
// these requests, so the Claude call and the thread save both run to completion regardless of
// the client reloading). GenerateArtifactAsync in Program.cs calls
// GenerationIdempotencyService.FindExistingAsync before doing any work — these tests cover
// that lookup directly, the same way OwnedRecordOwnershipCheckTests covers Program.cs's other
// explicit per-record checks without spinning up the actual HTTP pipeline.
public class GenerationIdempotencyServiceTests
{
    private const int OwnerUserId = 1;
    private const int OtherUserId = 2;

    private static DbContextOptions<AppDbContext> FreshOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static AgentThread MakeThread(int userId, string? clientRequestId, string content = "generated text") => new()
    {
        UserId = userId,
        ArtifactType = AgentThreadType.Cv,
        HistoryJson = "[]",
        CurrentContent = content,
        Status = AgentThreadStatus.Complete,
        ClientRequestId = clientRequestId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    // TC01 — No idempotency key supplied (older client, or a flow that doesn't use one): treated
    // as "nothing to replay," not an error, and never has to touch a tenant-scoped table (so this
    // must not throw AppDbContext's CurrentUserId-required guard).
    [Fact]
    public async Task FindExistingAsync_NullClientRequestId_ReturnsNullWithoutRequiringCurrentUserId()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var result = await GenerationIdempotencyService.FindExistingAsync(db, OwnerUserId, null);

        Assert.Null(result);
    }

    // TC02 — Blank/whitespace key: same as null, defensive against an empty string slipping
    // through instead of an actual GUID.
    [Fact]
    public async Task FindExistingAsync_BlankClientRequestId_ReturnsNull()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var result = await GenerationIdempotencyService.FindExistingAsync(db, OwnerUserId, "   ");

        Assert.Null(result);
    }

    // TC03 — The exact bug this exists to fix: a prior request already completed and saved a
    // thread under this key (the generation the user was charged for actually happened and
    // produced a result) — a resubmit with the same key must find it, so the frontend can
    // recover it after a refresh instead of the user re-clicking Generate and paying again.
    [Fact]
    public async Task FindExistingAsync_MatchingKeyForOwner_ReturnsThatThread()
    {
        var options = FreshOptions();
        int threadId;
        await using (var seed = new AppDbContext(options) { CurrentUserId = OwnerUserId })
        {
            var thread = MakeThread(OwnerUserId, "req-123");
            seed.AgentThreads.Add(thread);
            await seed.SaveChangesAsync();
            threadId = thread.Id;
        }

        await using var db = new AppDbContext(options) { CurrentUserId = OwnerUserId };
        var found = await GenerationIdempotencyService.FindExistingAsync(db, OwnerUserId, "req-123");

        Assert.NotNull(found);
        Assert.Equal(threadId, found!.Id);
    }

    // TC04 — Two different users can legitimately generate the same client-minted GUID key in
    // theory (however astronomically unlikely) — the lookup is still scoped per-user, so one
    // user's key can never resolve to another user's thread.
    [Fact]
    public async Task FindExistingAsync_SameKeyDifferentUser_ReturnsNull()
    {
        var options = FreshOptions();
        await using (var seed = new AppDbContext(options) { CurrentUserId = OtherUserId })
        {
            seed.AgentThreads.Add(MakeThread(OtherUserId, "shared-key"));
            await seed.SaveChangesAsync();
        }

        await using var db = new AppDbContext(options) { CurrentUserId = OwnerUserId };
        var found = await GenerationIdempotencyService.FindExistingAsync(db, OwnerUserId, "shared-key");

        Assert.Null(found);
    }

    // TC05 — No thread was ever saved under this key (it never got far enough — e.g. the
    // original attempt failed before saving, and WithCreditAsync already refunded it): a
    // resubmit with the same key must fall through to a normal (fresh) attempt, not error.
    [Fact]
    public async Task FindExistingAsync_UnknownKey_ReturnsNull()
    {
        var options = FreshOptions();
        await using (var seed = new AppDbContext(options) { CurrentUserId = OwnerUserId })
        {
            seed.AgentThreads.Add(MakeThread(OwnerUserId, "some-other-key"));
            await seed.SaveChangesAsync();
        }

        await using var db = new AppDbContext(options) { CurrentUserId = OwnerUserId };
        var found = await GenerationIdempotencyService.FindExistingAsync(db, OwnerUserId, "req-never-seen");

        Assert.Null(found);
    }

    // TC06 — The DB-level guard behind the idempotency check: AppDbContext.OnModelCreating must
    // declare a *unique* index on (UserId, ClientRequestId), so two threads can never both land
    // under the same owner + key even if two requests somehow raced past the FindExistingAsync
    // check at the same instant (the check-then-act window that check alone can't close). This
    // asserts the model configuration directly rather than trying to trigger a real conflict —
    // EF Core's InMemory provider (used everywhere else in this file) doesn't enforce unique
    // indexes the way Postgres does, so a "second save throws" version of this test would pass
    // even if `.IsUnique()` were accidentally deleted from AppDbContext; only the real Postgres
    // index (exercised by the AddAgentThreadClientRequestId migration) actually enforces this.
    [Fact]
    public void AgentThread_UserIdClientRequestIdIndex_IsUnique()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var entityType = db.Model.FindEntityType(typeof(AgentThread))!;
        var index = entityType.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "UserId", "ClientRequestId" }));

        Assert.True(index.IsUnique);
    }

    // TC07 — Two ordinary (non-idempotent) threads, both with ClientRequestId == null, must
    // coexist fine under the same user — this is the common case (only /cv and /letter set a key
    // at all; /answer and /threads/{id}/edit never do). Postgres treats every NULL as distinct
    // for uniqueness purposes, so the index above must never block this.
    [Fact]
    public async Task TwoThreadsWithNullClientRequestId_BothSaveSuccessfully()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options) { CurrentUserId = OwnerUserId };

        db.AgentThreads.Add(MakeThread(OwnerUserId, null, "first"));
        db.AgentThreads.Add(MakeThread(OwnerUserId, null, "second"));

        var exception = await Record.ExceptionAsync(() => db.SaveChangesAsync());

        Assert.Null(exception);
    }
}
