using JobSearch.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Api.Tests;

public class UserSecretServiceTests
{
    private static AppDbContext FreshDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // Ephemeral, in-process key ring — sufficient to test UserSecretService's own logic;
    // which storage backend the key ring uses is a separate, infrastructure-level concern.
    private static UserSecretService MakeService() =>
        new(DataProtectionProvider.Create("JobFindr.Tests"));

    // TC01 — A value set can be read back unchanged.
    [Fact]
    public async Task SetThenGet_RoundTripsOriginalValue()
    {
        using var db = FreshDb();
        var service = MakeService();

        await service.SetAsync(db, userId: 1, "gmail_refresh_token", "plaintext-token-value");
        var result = await service.GetAsync(db, userId: 1, "gmail_refresh_token");

        Assert.Equal("plaintext-token-value", result);
    }

    // TC02 — The stored row's EncryptedValue is not the plaintext.
    // Silent failure: if UserSecretService is ever changed to skip encryption (e.g. during
    // a refactor), GetAsync would still round-trip correctly in tests while the DB actually
    // stores tokens in plaintext — this test catches that specifically.
    [Fact]
    public async Task SetAsync_StoredValueIsNotPlaintext()
    {
        using var db = FreshDb();
        var service = MakeService();

        await service.SetAsync(db, userId: 1, "gmail_refresh_token", "plaintext-token-value");

        var stored = db.UserSecrets.Single();
        Assert.NotEqual("plaintext-token-value", stored.EncryptedValue);
    }

    // TC03 — Setting the same key twice updates the existing row rather than duplicating.
    // Silent failure: without this, re-running the GMAIL_REFRESH_TOKEN migration bridge on
    // every worker startup would insert a duplicate row and violate the unique index.
    [Fact]
    public async Task SetAsync_SameKeyTwice_UpdatesExistingRowWithoutDuplicating()
    {
        using var db = FreshDb();
        var service = MakeService();
        await service.SetAsync(db, userId: 1, "gmail_refresh_token", "first-value");

        await service.SetAsync(db, userId: 1, "gmail_refresh_token", "second-value");

        Assert.Single(db.UserSecrets);
        Assert.Equal("second-value", await service.GetAsync(db, userId: 1, "gmail_refresh_token"));
    }

    // TC04 — A key that was never set returns null, not an exception.
    [Fact]
    public async Task GetAsync_KeyNeverSet_ReturnsNull()
    {
        using var db = FreshDb();
        var service = MakeService();

        var result = await service.GetAsync(db, userId: 1, "gmail_refresh_token");

        Assert.Null(result);
    }

    // TC05 — The same key for two different users is stored and read independently.
    [Fact]
    public async Task SetAndGet_SameKeyDifferentUsers_ValuesAreIndependent()
    {
        using var db = FreshDb();
        var service = MakeService();
        await service.SetAsync(db, userId: 1, "gmail_refresh_token", "user-1-token");
        await service.SetAsync(db, userId: 2, "gmail_refresh_token", "user-2-token");

        Assert.Equal("user-1-token", await service.GetAsync(db, userId: 1, "gmail_refresh_token"));
        Assert.Equal("user-2-token", await service.GetAsync(db, userId: 2, "gmail_refresh_token"));
    }
}
