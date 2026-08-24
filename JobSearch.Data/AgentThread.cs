using Anthropic.Models.Messages;

namespace JobSearch.Data;

// A multi-turn Claude conversation. Backs both the /answer conversational Q&A flow and the
// /edit revision flow for CVs, cover letters, and answers — the caller already has the
// thread's own Id from the JSON response, so a follow-up (Q&A continuation or revision)
// always addresses it directly by Id.
public class AgentThread
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string ArtifactType { get; set; } = "";   // AgentThreadType
    public string HistoryJson { get; set; } = "[]";  // List<AgentThreadTurn>, System.Text.Json
    public string? CurrentContent { get; set; }       // latest assistant turn's content; null while AwaitingContext
    // Claims AccuracyVerifierAgent couldn't trace back to the candidate's own source material
    // for the current CurrentContent — List<string>, System.Text.Json. Null (not just empty)
    // until the first verification pass runs, so "never checked" stays distinguishable from
    // "checked, nothing flagged" if that distinction is ever needed later.
    public string? AccuracyWarningsJson { get; set; }
    public string Status { get; set; } = AgentThreadStatus.AwaitingContext;
    // Hiring company, if identifiable from the posting at generation time — used to name
    // downloaded CV/cover-letter files ("{Applicant} - {Company} - Resume.pdf"). Null when
    // not identifiable, or for Answer threads (no download exists for those).
    public string? Company { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public static class AgentThreadType
{
    public const string Cv          = "Cv";
    public const string CoverLetter = "CoverLetter";
    public const string Answer      = "Answer";
}

public static class AgentThreadStatus
{
    public const string AwaitingContext = "AwaitingContext";
    public const string Complete        = "Complete";
}

// Role is "user" or "assistant" — matches the Anthropic Messages API roles directly.
public record AgentThreadTurn(string Role, string Content);

public static class AgentThreadTurnExtensions
{
    public static List<MessageParam> ToMessages(this IReadOnlyList<AgentThreadTurn> history) =>
        [.. history.Select(t => new MessageParam
        {
            Role = t.Role == "assistant" ? Role.Assistant : Role.User,
            Content = t.Content,
        })];
}
