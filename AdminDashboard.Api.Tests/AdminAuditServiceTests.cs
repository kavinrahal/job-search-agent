using AdminDashboard.Api.Services;
using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

namespace AdminDashboard.Api.Tests;

public class AdminAuditServiceTests
{
    private static AppDbContext FreshDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task LogAsync_WritesOneRowWithGivenFields()
    {
        using var db = FreshDb();

        await AdminAuditService.LogAsync(db, AdminAuditActions.CreditAdjust, targetUserId: 42, "creditBalance: 4 -> 9");

        var row = Assert.Single(db.AdminAuditLogs);
        Assert.Equal(AdminAuditActions.CreditAdjust, row.Action);
        Assert.Equal(42, row.TargetUserId);
        Assert.Contains("creditBalance: 4 -> 9", row.Detail);
    }

    [Fact]
    public async Task LogAsync_PrefixesDetailWithTheActor()
    {
        using var db = FreshDb();

        await AdminAuditService.LogAsync(db, AdminAuditActions.WorkerLockCleared, targetUserId: null, "acquiredAt: null -> null");

        var row = Assert.Single(db.AdminAuditLogs);
        // Single-admin tool — "owner" is the only actor there is (see AdminAuditLog's own
        // doc comment), so every row should be attributable to it without a real identity.
        Assert.StartsWith("[owner]", row.Detail);
        Assert.Null(row.TargetUserId);
    }

    [Fact]
    public async Task LogAsync_StampsPerformedAtOnEveryCall()
    {
        using var db = FreshDb();
        var before = DateTime.UtcNow;

        await AdminAuditService.LogAsync(db, AdminAuditActions.Deactivate, 7, "deactivatedAt: null -> now");

        var row = Assert.Single(db.AdminAuditLogs);
        Assert.True(row.PerformedAt >= before);
    }

    [Fact]
    public async Task LogAsync_AlsoPersistsOtherPendingChangesOnTheSameContext()
    {
        // The Emergency page's OnPost handlers mutate an entity (e.g. User.CreditBalance) and
        // then call LogAsync without an intervening SaveChangesAsync — LogAsync's own
        // SaveChangesAsync has to flush both the mutation and the new audit row together.
        using var db = FreshDb();
        var user = new User { Email = "a@example.com", Tier = UserTier.Tier1, CreditBalance = 4, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        user.CreditBalance = 9;
        await AdminAuditService.LogAsync(db, AdminAuditActions.CreditAdjust, user.Id, "creditBalance: 4 -> 9");

        Assert.Equal(9, db.Users.Single(u => u.Id == user.Id).CreditBalance);
        Assert.Single(db.AdminAuditLogs);
    }
}
