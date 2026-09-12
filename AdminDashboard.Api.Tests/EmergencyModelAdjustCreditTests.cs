using AdminDashboard.Api.Pages;
using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

namespace AdminDashboard.Api.Tests;

// Covers OnPostAdjustCreditAsync, following the same style established by
// EmergencyModelReactivateTests — this action was switched from a numeric targetUserId to an
// email lookup (the admin dashboard's Users table has no way to find a user's numeric id, so
// requiring one made the form unusable in practice), so the email-normalization and
// not-found paths are the new behavior worth a check.
public class EmergencyModelAdjustCreditTests
{
    private static AppDbContext FreshDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options);

    private static async Task<(EmergencyModel model, AppDbContext writeDb)> NewModelAsync()
    {
        var dbName = Guid.NewGuid().ToString();
        var readDb = FreshDb(dbName);
        var writeDb = FreshDb(dbName);
        writeDb.SiteStatuses.Add(new SiteStatus { UpdatedAt = DateTime.UtcNow });
        await writeDb.SaveChangesAsync();

        return (new EmergencyModel(readDb, writeDb), writeDb);
    }

    [Fact]
    public async Task OnPostAdjustCreditAsync_WrongConfirmText_RejectsWithoutTouchingUser()
    {
        var (model, writeDb) = await NewModelAsync();
        var user = new User { Email = "a@example.com", Tier = UserTier.Tier1, CreditBalance = 5, CreatedAt = DateTime.UtcNow };
        writeDb.Users.Add(user);
        await writeDb.SaveChangesAsync();

        await model.OnPostAdjustCreditAsync(user.Email, amount: 5, confirmText: "nope");

        Assert.Equal(5, (await writeDb.Users.FindAsync(user.Id))!.CreditBalance);
        Assert.Empty(writeDb.AdminAuditLogs);
    }

    [Fact]
    public async Task OnPostAdjustCreditAsync_UnknownEmail_Rejects()
    {
        var (model, writeDb) = await NewModelAsync();

        await model.OnPostAdjustCreditAsync("nobody@example.com", amount: 5, confirmText: "CONFIRM");

        Assert.Empty(writeDb.AdminAuditLogs);
    }

    // Email lookup must be case/whitespace-insensitive, same normalization
    // UserProvisioningService.GetOrCreateAsync already applies at sign-in — an admin pasting an
    // email from somewhere else shouldn't need to match its exact stored casing.
    [Fact]
    public async Task OnPostAdjustCreditAsync_EmailDifferentCaseAndWhitespace_StillMatches()
    {
        var (model, writeDb) = await NewModelAsync();
        var user = new User { Email = "b@example.com", Tier = UserTier.Tier1, CreditBalance = 3, CreatedAt = DateTime.UtcNow };
        writeDb.Users.Add(user);
        await writeDb.SaveChangesAsync();

        await model.OnPostAdjustCreditAsync("  B@Example.com  ", amount: 2, confirmText: "CONFIRM");

        Assert.Equal(5, (await writeDb.Users.FindAsync(user.Id))!.CreditBalance);
    }

    [Fact]
    public async Task OnPostAdjustCreditAsync_NegativeDeltaBelowZero_FloorsAtZero()
    {
        var (model, writeDb) = await NewModelAsync();
        var user = new User { Email = "c@example.com", Tier = UserTier.Tier1, CreditBalance = 2, CreatedAt = DateTime.UtcNow };
        writeDb.Users.Add(user);
        await writeDb.SaveChangesAsync();

        await model.OnPostAdjustCreditAsync(user.Email, amount: -10, confirmText: "CONFIRM");

        Assert.Equal(0, (await writeDb.Users.FindAsync(user.Id))!.CreditBalance);
    }

    [Fact]
    public async Task OnPostAdjustCreditAsync_ValidRequest_AdjustsBalanceAndLogsAudit()
    {
        var (model, writeDb) = await NewModelAsync();
        var user = new User { Email = "d@example.com", Tier = UserTier.Tier2, CreditBalance = 10, CreatedAt = DateTime.UtcNow };
        writeDb.Users.Add(user);
        await writeDb.SaveChangesAsync();

        var result = await model.OnPostAdjustCreditAsync(user.Email, amount: 5, confirmText: "CONFIRM");

        Assert.Equal(15, (await writeDb.Users.FindAsync(user.Id))!.CreditBalance);
        var log = Assert.Single(writeDb.AdminAuditLogs);
        Assert.Equal(AdminAuditActions.CreditAdjust, log.Action);
        Assert.Equal(user.Id, log.TargetUserId);
        Assert.IsAssignableFrom<Microsoft.AspNetCore.Mvc.IActionResult>(result);
        Assert.IsNotType<Microsoft.AspNetCore.Mvc.RazorPages.PageResult>(result);
    }
}
