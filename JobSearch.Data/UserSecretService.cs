using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Data;

public class UserSecretService
{
    // Distinct purpose string so this protector's ciphertext can't be swapped in for any
    // other IDataProtector's output in the same key ring, even by accident.
    private const string Purpose = "JobFindr.UserSecrets.v1";

    private readonly IDataProtector _protector;

    public UserSecretService(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector(Purpose);

    public async Task SetAsync(AppDbContext db, int userId, string key, string plaintextValue)
    {
        var encrypted = _protector.Protect(plaintextValue);
        var existing = await db.UserSecrets.FirstOrDefaultAsync(s => s.UserId == userId && s.Key == key);
        if (existing is not null)
        {
            existing.EncryptedValue = encrypted;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            db.UserSecrets.Add(new UserSecret { UserId = userId, Key = key, EncryptedValue = encrypted, UpdatedAt = DateTime.UtcNow });
        }
        await db.SaveChangesAsync();
    }

    public async Task<string?> GetAsync(AppDbContext db, int userId, string key)
    {
        var secret = await db.UserSecrets.FirstOrDefaultAsync(s => s.UserId == userId && s.Key == key);
        return secret is null ? null : _protector.Unprotect(secret.EncryptedValue);
    }

    // Instance method for the same reason as SetAsync/GetAsync — API symmetry as a matched
    // Set/Get/Delete trio callers use the same way — even though this one never touches
    // _protector.
#pragma warning disable S2325
    public async Task DeleteAsync(AppDbContext db, int userId, string key)
    {
        var secret = await db.UserSecrets.FirstOrDefaultAsync(s => s.UserId == userId && s.Key == key);
        if (secret is null) return;
        db.UserSecrets.Remove(secret);
        await db.SaveChangesAsync();
    }
#pragma warning restore S2325
}
