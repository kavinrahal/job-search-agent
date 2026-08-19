using Microsoft.EntityFrameworkCore;

namespace JobSearch.Data;

// HasCreditAsync is only ever a fail-fast optimism check (skip fetch/cross-check work for a
// user who's obviously out of credits) — the real gate is SpendCreditAsync's return value,
// called immediately before the Claude call it pays for, not after. Calling it early and
// ignoring the result (the previous design) let concurrent requests all pass HasCreditAsync
// before any of them actually spent, each triggering its own real Claude call for the cost
// of one credit.
public static class CreditService
{
    public static async Task<bool> HasCreditAsync(AppDbContext db, int userId)
    {
        var user = await db.Users.FindAsync(userId);
        return user is not null && user.CreditBalance > 0;
    }

    // Guarded by User.CreditVersion, an optimistic concurrency token — two concurrent requests
    // that both passed HasCreditAsync with 1 credit left can no longer both commit a decrement:
    // whichever SaveChangesAsync lands first bumps the version, and the second's save then
    // fails with DbUpdateConcurrencyException instead of silently overwriting it. Callers must
    // check the return value and skip the paid call entirely on false — see the class comment.
    public static async Task<bool> SpendCreditAsync(AppDbContext db, int userId)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null || user.CreditBalance <= 0) return false;

        user.CreditBalance -= 1;
        user.CreditVersion += 1;
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    // Spending now happens before generation (see above), which means a Claude call that
    // throws, or a save that fails afterward, needs to give the credit back rather than
    // charging the user for a request that produced nothing. A lost refund under a rare
    // concurrent double-failure just costs the user one credit — not worth a retry loop for.
    public static async Task RefundCreditAsync(AppDbContext db, int userId)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null) return;

        user.CreditBalance += 1;
        user.CreditVersion += 1;
        try
        {
            await db.SaveChangesAsync();
        }
#pragma warning disable S108 // swallow intentionally — see the method comment above
        catch (DbUpdateConcurrencyException)
        {
        }
#pragma warning restore S108
    }
}
