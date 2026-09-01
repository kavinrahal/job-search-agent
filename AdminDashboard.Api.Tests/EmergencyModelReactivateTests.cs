using AdminDashboard.Api.Pages;
using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

namespace AdminDashboard.Api.Tests;

// Covers the confirm-gate/audit-log behavior of OnPostReactivateAsync directly against
// EmergencyModel, the same way AdminAuditServiceTests exercises AdminAuditService directly —
// no PageModel test convention exists yet in this repo for the Emergency page's other six
// actions, so this follows the same in-memory-db, construct-the-class-directly style already
// established for the rest of AdminDashboard.Api.Tests rather than inventing a new one.
public class EmergencyModelReactivateTests
{
    private static AppDbContext FreshDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options);

    // Read and write contexts share one in-memory database, same relationship as production's
    // AdminDbContextKeys.Read/Write (two connections to the same database), and LoadDisplayDataAsync
    // (called on the Invalid() path) requires a SiteStatus row to exist, same as the real singleton.
    private static async Task<(EmergencyModel model, AppDbContext writeDb)> NewModelAsync()
    {
        var dbName = Guid.NewGuid().ToString();
        var readDb = FreshDb(dbName);
        var writeDb = FreshDb(dbName);
        writeDb.SiteStatuses.Add(new SiteStatus { UpdatedAt = DateTime.UtcNow });
        await writeDb.SaveChangesAsync();

        return (new EmergencyModel(readDb, writeDb), writeDb);
    }

    // TC01 — Wrong/missing confirm text is rejected before any DB access, matching every
    // other Emergency action's first-line gate.
    [Fact]
    public async Task OnPostReactivateAsync_WrongConfirmText_RejectsWithoutTouchingUser()
    {
        var (model, writeDb) = await NewModelAsync();
        var user = new User { Email = "a@example.com", Tier = UserTier.Tier1, DeactivatedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow };
        writeDb.Users.Add(user);
        await writeDb.SaveChangesAsync();

        await model.OnPostReactivateAsync(user.Id, confirmText: "nope");

        Assert.NotNull((await writeDb.Users.FindAsync(user.Id))!.DeactivatedAt);
        Assert.Empty(writeDb.AdminAuditLogs);
    }

    // TC02 — Unknown user id is rejected with the same "No user with id {id}." pattern the
    // other five user-targeted actions use.
    [Fact]
    public async Task OnPostReactivateAsync_UnknownUser_Rejects()
    {
        var (model, writeDb) = await NewModelAsync();

        await model.OnPostReactivateAsync(targetUserId: 999, confirmText: "CONFIRM");

        Assert.Empty(writeDb.AdminAuditLogs);
    }

    // TC03 — A user who isn't currently deactivated is rejected cleanly rather than silently
    // no-oping or logging a misleading audit row.
    [Fact]
    public async Task OnPostReactivateAsync_UserNotDeactivated_Rejects()
    {
        var (model, writeDb) = await NewModelAsync();
        var user = new User { Email = "active@example.com", Tier = UserTier.Tier1, DeactivatedAt = null, CreatedAt = DateTime.UtcNow };
        writeDb.Users.Add(user);
        await writeDb.SaveChangesAsync();

        await model.OnPostReactivateAsync(user.Id, confirmText: "CONFIRM");

        Assert.Empty(writeDb.AdminAuditLogs);
    }

    // TC04 — The success path: DeactivatedAt is cleared and exactly one Reactivate audit row
    // is written, same one-mutation-one-audit-row contract as every other action.
    [Fact]
    public async Task OnPostReactivateAsync_DeactivatedUser_ClearsDeactivatedAtAndLogsAudit()
    {
        var (model, writeDb) = await NewModelAsync();
        var user = new User { Email = "b@example.com", Tier = UserTier.Tier2, DeactivatedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow };
        writeDb.Users.Add(user);
        await writeDb.SaveChangesAsync();

        var result = await model.OnPostReactivateAsync(user.Id, confirmText: "CONFIRM");

        Assert.Null((await writeDb.Users.FindAsync(user.Id))!.DeactivatedAt);
        var log = Assert.Single(writeDb.AdminAuditLogs);
        Assert.Equal(AdminAuditActions.Reactivate, log.Action);
        Assert.Equal(user.Id, log.TargetUserId);
        // Redirects rather than returning Page() — a refresh after a successful action must
        // not resubmit it (same reasoning as every other action's Success()).
        Assert.IsAssignableFrom<Microsoft.AspNetCore.Mvc.IActionResult>(result);
        Assert.IsNotType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(result);
    }
}
