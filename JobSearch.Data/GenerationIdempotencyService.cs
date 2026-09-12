using Microsoft.EntityFrameworkCore;

namespace JobSearch.Data;

// Backs the /cv and /letter generation endpoints' idempotency-key replay path (see
// AgentThread.ClientRequestId's own comment for why this exists — a page refresh while
// generation is in flight loses the response, not the server-side work, which keeps running
// and completing with nothing wired to cancel it). A caller that resubmits the same
// ClientRequestId gets back whatever that key already produced instead of spending a second
// credit and running a second Claude call for what is, from the user's perspective, one
// generation.
public static class GenerationIdempotencyService
{
    // Null/blank key: no idempotency requested (older clients, or /answer which doesn't use
    // this), so callers should proceed as if nothing was found.
    public static Task<AgentThread?> FindExistingAsync(AppDbContext db, int userId, string? clientRequestId)
    {
        if (string.IsNullOrWhiteSpace(clientRequestId)) return Task.FromResult<AgentThread?>(null);

        return db.AgentThreads
            .Where(t => t.UserId == userId && t.ClientRequestId == clientRequestId)
            .FirstOrDefaultAsync();
    }
}
