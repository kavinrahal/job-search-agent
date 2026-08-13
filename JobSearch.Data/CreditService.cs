using Microsoft.EntityFrameworkCore;

namespace JobSearch.Data;

// Checked before generating (skip the API call entirely if there's nothing to spend) and
// applied only after a successful result — never before/during generation, and never on
// failure, so there's no refund path to build.
// ponytail: no concurrency guard against two simultaneous requests both passing the check
// with 1 credit left — real risk once a user can have multiple tabs/devices open; add
// row-level locking or an atomic UPDATE...WHERE CreditBalance > 0 if that shows up in practice.
public static class CreditService
{
    public static async Task<bool> HasCreditAsync(AppDbContext db, int userId)
    {
        var user = await db.Users.FindAsync(userId);
        return user is not null && user.CreditBalance > 0;
    }

    public static async Task SpendCreditAsync(AppDbContext db, int userId)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null) return;
        user.CreditBalance -= 1;
        await db.SaveChangesAsync();
    }
}
