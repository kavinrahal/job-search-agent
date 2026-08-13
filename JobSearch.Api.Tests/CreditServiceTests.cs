using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Api.Tests;

public class CreditServiceTests
{
    private static AppDbContext FreshDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<User> SeedUser(AppDbContext db, int creditBalance)
    {
        var user = new User { Email = "test@example.com", Tier = UserTier.Tier1, CreditBalance = creditBalance, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    // TC01 — A user with credits remaining has credit.
    [Fact]
    public async Task HasCreditAsync_PositiveBalance_ReturnsTrue()
    {
        using var db = FreshDb();
        var user = await SeedUser(db, creditBalance: 5);

        Assert.True(await CreditService.HasCreditAsync(db, user.Id));
    }

    // TC02 — A user with zero balance has no credit.
    // Silent failure: this is the actual enforcement gate — if it wrongly returns true at
    // zero, generation endpoints would let a user with no credits keep generating for free.
    [Fact]
    public async Task HasCreditAsync_ZeroBalance_ReturnsFalse()
    {
        using var db = FreshDb();
        var user = await SeedUser(db, creditBalance: 0);

        Assert.False(await CreditService.HasCreditAsync(db, user.Id));
    }

    // TC03 — A nonexistent user has no credit (fails closed, not an exception).
    [Fact]
    public async Task HasCreditAsync_UnknownUser_ReturnsFalse()
    {
        using var db = FreshDb();

        Assert.False(await CreditService.HasCreditAsync(db, userId: 999));
    }

    // TC04 — Spending decrements the balance by exactly 1.
    [Fact]
    public async Task SpendCreditAsync_DecrementsBalanceByOne()
    {
        using var db = FreshDb();
        var user = await SeedUser(db, creditBalance: 5);

        await CreditService.SpendCreditAsync(db, user.Id);

        var updated = await db.Users.FindAsync(user.Id);
        Assert.Equal(4, updated!.CreditBalance);
    }

    // TC05 — Spending for a nonexistent user is a no-op, not an exception.
    // This matters because the calling endpoints only ever spend after HasCreditAsync
    // already confirmed the user exists with a positive balance — a missing-user case here
    // would be a bug elsewhere, and this must not crash the response that's already been
    // generated for the caller.
    [Fact]
    public async Task SpendCreditAsync_UnknownUser_DoesNotThrow()
    {
        using var db = FreshDb();

        var exception = await Record.ExceptionAsync(() => CreditService.SpendCreditAsync(db, userId: 999));

        Assert.Null(exception);
    }

    // TC06 — Balance can go to exactly zero (the boundary HasCreditAsync then rejects).
    [Fact]
    public async Task SpendCreditAsync_LastCredit_BalanceReachesZero()
    {
        using var db = FreshDb();
        var user = await SeedUser(db, creditBalance: 1);

        await CreditService.SpendCreditAsync(db, user.Id);

        Assert.False(await CreditService.HasCreditAsync(db, user.Id));
    }
}
