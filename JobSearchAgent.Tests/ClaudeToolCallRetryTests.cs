using System.Net;
using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using JobSearch.Data;

namespace JobSearchAgent.Tests;

// Exercises ClaudeToolCallRetry.CallAsync against a fake HTTP transport (AnthropicClient.HttpClient
// is settable, so a real client can be pointed at canned responses without a live API key or
// network call) rather than a hand-rolled fake of the SDK itself — there's no existing
// mock-AnthropicClient convention in this repo to match (the existing "Fake*Agent" test doubles
// in JobAlertProcessorTests subclass the public agent API, one layer above where this helper
// lives), and this approach exercises the real request/response (de)serialization path too.
public class ClaudeToolCallRetryTests
{
    private const string ToolName = "test_tool";

    private static AnthropicClient MakeClient(FakeHandler handler) => new()
    {
        ApiKey = "test-key",
        HttpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.test/") },
        MaxRetries = 0, // the SDK's own transport-level retry would obscure the attempt count below
    };

    private static MessageCreateParams BuildRequest(IReadOnlyList<MessageParam> messages) => new()
    {
        Model = "claude-haiku-4-5",
        MaxTokens = 64,
        Tools = [new Tool
        {
            Name = ToolName,
            Description = "Test tool.",
            InputSchema = new InputSchema
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["value"] = JsonSerializer.SerializeToElement(new { type = "string" }),
                },
                Required = ["value"],
            },
        }],
        ToolChoice = new ToolChoiceAny(),
        Messages = [.. messages],
    };

    private static string ValidToolUseResponse(string value) => $$"""
        {
          "id": "msg_valid",
          "type": "message",
          "role": "assistant",
          "model": "claude-haiku-4-5",
          "content": [ { "type": "tool_use", "id": "toolu_1", "name": "{{ToolName}}", "input": { "value": "{{value}}" } } ],
          "stop_reason": "tool_use",
          "stop_sequence": null,
          "usage": { "input_tokens": 10, "output_tokens": 5 }
        }
        """;

    private const string MalformedResponse = """
        {
          "id": "msg_malformed",
          "type": "message",
          "role": "assistant",
          "model": "claude-haiku-4-5",
          "content": [ { "type": "text", "text": "Sorry, I can't help with that." } ],
          "stop_reason": "end_turn",
          "stop_sequence": null,
          "usage": { "input_tokens": 8, "output_tokens": 4 }
        }
        """;

    private static string ParseValue(IReadOnlyDictionary<string, JsonElement> input) =>
        input["value"].GetString() ?? "";

    // TC01 — First attempt returns a valid tool_use block: no retry happens.
    [Fact]
    public async Task CallAsync_FirstAttemptValid_ReturnsResultWithoutRetrying()
    {
        var handler = new FakeHandler([ValidToolUseResponse("first-try")]);
        var client = MakeClient(handler);

        var result = await ClaudeToolCallRetry.CallAsync(
            client, BuildRequest, initialMessages: [new() { Role = Role.User, Content = "hi" }],
            toolName: ToolName, parse: ParseValue,
            missingToolUseMessage: "missing tool use", logLabel: "Test");

        Assert.Equal("first-try", result);
        Assert.Equal(1, handler.CallCount);
    }

    // TC02 — First attempt malformed (no tool_use block), second attempt valid: the retry recovers.
    [Fact]
    public async Task CallAsync_FirstAttemptMalformed_RetriesAndSucceeds()
    {
        var handler = new FakeHandler([MalformedResponse, ValidToolUseResponse("second-try")]);
        var client = MakeClient(handler);

        var result = await ClaudeToolCallRetry.CallAsync(
            client, BuildRequest, initialMessages: [new() { Role = Role.User, Content = "hi" }],
            toolName: ToolName, parse: ParseValue,
            missingToolUseMessage: "missing tool use", logLabel: "Test");

        Assert.Equal("second-try", result);
        Assert.Equal(2, handler.CallCount);
    }

    // TC03 — Both attempts malformed: throws the caller-supplied message, exactly one retry (not
    // an unbounded loop) — the handler is invoked exactly twice, never a third time.
    [Fact]
    public async Task CallAsync_BothAttemptsMalformed_ThrowsAfterExactlyOneRetry()
    {
        var handler = new FakeHandler([MalformedResponse, MalformedResponse]);
        var client = MakeClient(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ClaudeToolCallRetry.CallAsync(
            client, BuildRequest, initialMessages: [new() { Role = Role.User, Content = "hi" }],
            toolName: ToolName, parse: ParseValue,
            missingToolUseMessage: "missing tool use", logLabel: "Test"));

        Assert.Equal("missing tool use", ex.Message);
        Assert.Equal(2, handler.CallCount);
    }

    // TC04 — A tool_use block is present both times, but its input never satisfies `parse`: the
    // original parse exception propagates (not a generic "no tool use block" message) after one
    // retry, and the caller's usage callback still fires for both real API calls.
    [Fact]
    public async Task CallAsync_ToolUsePresentButUnparseable_PropagatesParseExceptionAfterRetry()
    {
        var handler = new FakeHandler([ValidToolUseResponse("x"), ValidToolUseResponse("x")]);
        var client = MakeClient(handler);
        int usageCalls = 0;

        var ex = await Assert.ThrowsAsync<FormatException>(() => ClaudeToolCallRetry.CallAsync<string>(
            client, BuildRequest, initialMessages: [new() { Role = Role.User, Content = "hi" }],
            toolName: ToolName,
            parse: _ => throw new FormatException("value must be a number"),
            missingToolUseMessage: "missing tool use", logLabel: "Test",
            onUsage: _ => { usageCalls++; return Task.CompletedTask; }));

        Assert.Equal("value must be a number", ex.Message);
        Assert.Equal(2, handler.CallCount);
        Assert.Equal(2, usageCalls);
    }

    private sealed class FakeHandler(List<string> responseBodies) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = responseBodies[Math.Min(CallCount, responseBodies.Count - 1)];
            CallCount++;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }
}
