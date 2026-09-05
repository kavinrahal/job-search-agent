using Anthropic.Models.Messages;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Data;

// Fresh AppDbContext per call (not a shared long-lived one — this is used from a DI
// singleton in the API and from a plain script in the worker, neither of which should hold
// one instance across the whole process lifetime). Logging failures are swallowed: a usage
// log write must never break the CV/letter/evaluation/classification call it's attached to.
public class ClaudeUsageLogger(DbContextOptions<AppDbContext> dbOptions)
{
    public async Task LogAsync(int userId, string agentName, string model, Usage usage, string? skillVersion = null)
    {
        try
        {
            await using var db = new AppDbContext(dbOptions);
            db.CurrentUserId = userId;
            db.ClaudeUsageLogs.Add(new ClaudeUsageLog
            {
                UserId = userId,
                AgentName = agentName,
                Model = model,
                InputTokens = usage.InputTokens,
                OutputTokens = usage.OutputTokens,
                CacheReadInputTokens = usage.CacheReadInputTokens ?? 0,
                CacheCreationInputTokens = usage.CacheCreationInputTokens ?? 0,
                SkillVersion = skillVersion,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[ClaudeUsageLogger] Failed to log usage for {agentName}: {ex.Message}");
        }
    }
}
