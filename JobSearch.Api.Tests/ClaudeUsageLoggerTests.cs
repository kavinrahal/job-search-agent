using System.Text.Json;
using Anthropic.Models.Messages;
using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

namespace JobSearch.Api.Tests;

public class ClaudeUsageLoggerTests
{
    private static DbContextOptions<AppDbContext> FreshOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    // Usage has several `required` members unrelated to what ClaudeUsageLogger reads
    // (CacheCreation, ServiceTier, etc.), which JSON deserialization satisfies without
    // needing every field present — the same path the SDK itself uses on a real API
    // response, so this is also a more realistic fixture than a manual object initializer.
    private static Usage MakeUsage(long input = 100, long output = 50, long? cacheRead = null, long? cacheCreation = null) =>
        JsonSerializer.Deserialize<Usage>($$"""
            {
                "input_tokens": {{input}},
                "output_tokens": {{output}},
                "cache_read_input_tokens": {{(cacheRead.HasValue ? cacheRead.Value.ToString() : "null")}},
                "cache_creation_input_tokens": {{(cacheCreation.HasValue ? cacheCreation.Value.ToString() : "null")}}
            }
            """)!;

    // TC01 — A logged call is persisted with the given fields.
    [Fact]
    public async Task LogAsync_WritesRowWithGivenFields()
    {
        var options = FreshOptions();
        var logger = new ClaudeUsageLogger(options);

        await logger.LogAsync(1, ClaudeAgentName.PostingEvaluator, "claude-opus-4-8", MakeUsage(input: 200, output: 80));

        await using var verify = new AppDbContext(options) { CurrentUserId = 1 };
        var log = Assert.Single(verify.ClaudeUsageLogs);
        Assert.Equal(1, log.UserId);
        Assert.Equal(ClaudeAgentName.PostingEvaluator, log.AgentName);
        Assert.Equal("claude-opus-4-8", log.Model);
        Assert.Equal(200, log.InputTokens);
        Assert.Equal(80, log.OutputTokens);
    }

    // TC02 — Null cache token fields (Anthropic omits them when caching wasn't used) are
    // stored as 0, not left to blow up on a non-nullable long column.
    [Fact]
    public async Task LogAsync_NullCacheTokens_StoredAsZero()
    {
        var options = FreshOptions();
        var logger = new ClaudeUsageLogger(options);

        await logger.LogAsync(1, ClaudeAgentName.EmailClassifier, "claude-haiku-4-5", MakeUsage(cacheRead: null, cacheCreation: null));

        await using var verify = new AppDbContext(options) { CurrentUserId = 1 };
        var log = Assert.Single(verify.ClaudeUsageLogs);
        Assert.Equal(0, log.CacheReadInputTokens);
        Assert.Equal(0, log.CacheCreationInputTokens);
    }

    // TC03 — Usage logs are append-only: repeated calls create separate rows, not an upsert.
    // Silent failure: if this ever behaved like UserProfile/UserSecret's get-or-create
    // pattern, every call after the first would silently overwrite cost history instead of
    // accumulating it.
    [Fact]
    public async Task LogAsync_MultipleCalls_AppendsRowsRatherThanOverwriting()
    {
        var options = FreshOptions();
        var logger = new ClaudeUsageLogger(options);

        await logger.LogAsync(1, ClaudeAgentName.CvTailorAgent, "claude-opus-4-8", MakeUsage());
        await logger.LogAsync(1, ClaudeAgentName.CvTailorAgent, "claude-opus-4-8", MakeUsage());

        await using var verify = new AppDbContext(options) { CurrentUserId = 1 };
        Assert.Equal(2, verify.ClaudeUsageLogs.Count());
    }

    // TC04 — A DB write failure is swallowed, never propagated to the calling agent.
    // This is the core design promise: a usage-log write must never break CV/letter/
    // evaluation/classification generation for the user.
    [Fact]
    public async Task LogAsync_DbWriteFails_DoesNotThrow()
    {
        var badOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=256.256.256.256;Database=x;Username=x;Password=x;Timeout=1")
            .Options;
        var logger = new ClaudeUsageLogger(badOptions);

        var exception = await Record.ExceptionAsync(() =>
            logger.LogAsync(1, ClaudeAgentName.PostingEvaluator, "claude-opus-4-8", MakeUsage()));

        Assert.Null(exception);
    }
}
