using System.Text.Json;
using JobSearch.Data;

namespace JobSearch.Api;

// The persisted state of an AgentThread, shaped to match the generation endpoints' JSON
// (threadId/text/mode/accuracyWarnings). GET /threads/{id} returns this so the frontend can drop
// it straight into the same result state a fresh generation produces — used to restore a
// just-generated CV/cover letter after an accidental refresh.
public record ThreadStateResponse(int ThreadId, string Mode, string? Text, string[] AccuracyWarnings)
{
    public static ThreadStateResponse From(AgentThread thread)
    {
        var mode = thread.Status == AgentThreadStatus.Complete ? "final_answer" : "ask_followup";
        var warnings = thread.AccuracyWarningsJson is null
            ? []
            : JsonSerializer.Deserialize<string[]>(thread.AccuracyWarningsJson) ?? [];
        return new ThreadStateResponse(thread.Id, mode, thread.CurrentContent, warnings);
    }
}
