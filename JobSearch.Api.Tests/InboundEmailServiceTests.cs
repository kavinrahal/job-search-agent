using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Api.Tests;

public class InboundEmailServiceTests
{
    private static AppDbContext FreshDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<int> SeedUserAsync(AppDbContext db, string? token = null)
    {
        var user = new User { Email = $"{Guid.NewGuid()}@example.com", CreatedAt = DateTime.UtcNow, InboundEmailToken = token };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    // TC01 — First call for a user with no token generates and persists one.
    [Fact]
    public async Task GetOrCreateAddressAsync_NoExistingToken_GeneratesAndPersistsOne()
    {
        using var db = FreshDb();
        var userId = await SeedUserAsync(db);

        var address = await InboundEmailService.GetOrCreateAddressAsync(db, userId, "alerts.example.com");

        var user = await db.Users.FindAsync(userId);
        Assert.NotNull(user!.InboundEmailToken);
        Assert.Equal($"{user.InboundEmailToken}@alerts.example.com", address);
    }

    // TC02 — Repeat calls return the same address rather than rotating the token.
    // Silent failure: without this, every call would mint a new address, breaking any
    // filter/forwarding rule the user already set up against the previous one.
    [Fact]
    public async Task GetOrCreateAddressAsync_RepeatCall_ReturnsStableAddress()
    {
        using var db = FreshDb();
        var userId = await SeedUserAsync(db);

        var first = await InboundEmailService.GetOrCreateAddressAsync(db, userId, "alerts.example.com");
        var second = await InboundEmailService.GetOrCreateAddressAsync(db, userId, "alerts.example.com");

        Assert.Equal(first, second);
    }

    // TC03 — Two different users never collide on the same generated token.
    [Fact]
    public async Task GenerateToken_CalledManyTimes_NeverCollides()
    {
        var tokens = Enumerable.Range(0, 1000).Select(_ => InboundEmailService.GenerateToken()).ToHashSet();
        Assert.Equal(1000, tokens.Count);
    }

    // TC04 — Resolves the correct user from a plain "token@domain" To value.
    [Fact]
    public async Task ResolveUserIdAsync_PlainAddress_ResolvesMatchingUser()
    {
        using var db = FreshDb();
        var userId = await SeedUserAsync(db, "abc123");

        var resolved = await InboundEmailService.ResolveUserIdAsync(db, "abc123@alerts.example.com");

        Assert.Equal(userId, resolved);
    }

    // TC05 — Resolves correctly when SendGrid sends the display-name form.
    [Fact]
    public async Task ResolveUserIdAsync_DisplayNameForm_ResolvesMatchingUser()
    {
        using var db = FreshDb();
        var userId = await SeedUserAsync(db, "abc123");

        var resolved = await InboundEmailService.ResolveUserIdAsync(db, "Job Alerts <abc123@alerts.example.com>");

        Assert.Equal(userId, resolved);
    }

    // TC06 — A token that doesn't match any user resolves to null rather than throwing.
    // Silent failure: this is the path a stale address or misdirected mail takes; if it
    // threw, one bad inbound email would crash the whole webhook handler.
    [Fact]
    public async Task ResolveUserIdAsync_UnknownToken_ReturnsNull()
    {
        using var db = FreshDb();
        await SeedUserAsync(db, "real-token");

        var resolved = await InboundEmailService.ResolveUserIdAsync(db, "nonexistent@alerts.example.com");

        Assert.Null(resolved);
    }

    // TC07 — Malformed "To" values (no "@") don't crash the lookup.
    [Fact]
    public async Task ResolveUserIdAsync_MalformedAddress_ReturnsNull()
    {
        using var db = FreshDb();

        var resolved = await InboundEmailService.ResolveUserIdAsync(db, "not-an-email");

        Assert.Null(resolved);
    }
}
