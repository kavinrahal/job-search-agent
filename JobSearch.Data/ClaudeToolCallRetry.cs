using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace JobSearch.Data;

// Shared retry wrapper for the "forced tool-use" Claude call pattern (see
// architecture-conventions.md's "Structured output from Claude" section). Every agent that
// forces a tool call (ToolChoiceAny + one Tool) throws InvalidOperationException today if the
// response comes back without the expected tool_use block, or with one whose Input doesn't
// parse into the shape the caller needs — no retry. In production this is usually a one-off
// model hiccup, not a deterministic failure, so a single retry with the specific problem
// explained back to the model recovers most of them. Exactly one retry, no backoff, no
// configurable retry count — matches this codebase's preference for a fixed constant over a
// knob nobody will tune (see CLAUDE.md).
public static class ClaudeToolCallRetry
{
    private const int MaxAttempts = 2; // the original call plus exactly one retry

    // buildRequest gets the running message list (grows by two entries — the malformed
    // assistant turn plus the corrective user turn — if a retry happens) and must return a
    // fresh MessageCreateParams for that attempt. initialMessages is never mutated by this
    // method; callers can safely reuse the list they passed in afterward.
    //
    // parse converts a successfully-found tool_use block's Input into TResult. Any exception it
    // throws is treated as "malformed input" — same as a missing tool_use block — and triggers
    // the retry (or, on the final attempt, propagates unchanged so callers see the original,
    // specific failure rather than a generic one).
    //
    // missingToolUseMessage is the exact message to throw (as InvalidOperationException, same
    // as today) if no attempt ever produces a tool_use block for toolName. logLabel identifies
    // the caller in the retry-observability log line.
    public static async Task<TResult> CallAsync<TResult>(
        AnthropicClient client,
        Func<IReadOnlyList<MessageParam>, MessageCreateParams> buildRequest,
        IReadOnlyList<MessageParam> initialMessages,
        string toolName,
        Func<IReadOnlyDictionary<string, JsonElement>, TResult> parse,
        string missingToolUseMessage,
        string logLabel,
        Func<Usage, Task>? onUsage = null)
    {
        var messages = new List<MessageParam>(initialMessages);

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var response = await client.Messages.Create(buildRequest(messages));
            if (onUsage is not null)
                await onUsage(response.Usage);

            // Logs the retry and appends the malformed-turn replay + corrective feedback that
            // the next loop iteration's buildRequest(messages) call will send.
            async Task QueueRetry(string logDetail, string feedback)
            {
                await Console.Error.WriteLineAsync($"[ClaudeToolCallRetry] {logLabel}: {logDetail} — retrying once.");
                messages.Add(ReplayAssistantTurn(response.Content));
                messages.Add(new MessageParam { Role = Role.User, Content = feedback });
            }

            ToolUseBlock? toolUse = null;
            foreach (var block in response.Content)
                if (block.TryPickToolUse(out ToolUseBlock? tu)) { toolUse = tu; break; }

            bool isLastAttempt = attempt == MaxAttempts;

            if (toolUse is not null)
            {
                if (isLastAttempt)
                    return parse(toolUse.Input); // let a still-bad shape surface its own specific message

                try
                {
                    return parse(toolUse.Input);
                }
                catch (Exception ex)
                {
                    await QueueRetry(
                        $"\"{toolName}\" tool call's input was invalid on attempt {attempt} ({ex.Message})",
                        $"Your previous \"{toolName}\" tool call's input was invalid: {ex.Message} Please retry with a corrected \"{toolName}\" tool call.");
                    continue;
                }
            }

            if (isLastAttempt)
                throw new InvalidOperationException(missingToolUseMessage);

            await QueueRetry(
                $"attempt {attempt} didn't include a \"{toolName}\" tool call",
                $"Your previous response didn't include a valid \"{toolName}\" tool call — please retry with the correct tool call format.");
        }

        // Unreachable: the loop above always either returns or throws by the final attempt.
        throw new InvalidOperationException(missingToolUseMessage);
    }

    // Replays whatever the model actually said (text and/or a tool call) back into the
    // conversation as its own turn, so the follow-up correction reads as a normal multi-turn
    // conversation rather than a non sequitur.
    private static MessageParam ReplayAssistantTurn(IReadOnlyList<ContentBlock> blocks)
    {
        var replayed = new List<ContentBlockParam>();
        foreach (var block in blocks)
        {
            if (block.TryPickText(out TextBlock? text))
                replayed.Add(new TextBlockParam { Text = text.Text });
            else if (block.TryPickToolUse(out ToolUseBlock? toolUse))
                replayed.Add(new ToolUseBlockParam { ID = toolUse.ID, Name = toolUse.Name, Input = toolUse.Input });
        }

        return new MessageParam { Role = Role.Assistant, Content = replayed };
    }
}
