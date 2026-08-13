using Anthropic.Models.Messages;

namespace JobSearch.Data;

// A multi-turn Claude conversation tracked by the Telegram message id of the bot's most
// recent reply in it, so the next reply from the candidate can be matched back to it.
// Backs both the /answer conversational Q&A flow and the /edit revision flow for CVs,
// cover letters, and answers.
public class AgentThread
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string ArtifactType { get; set; } = "";   // AgentThreadType
    public string HistoryJson { get; set; } = "[]";  // List<AgentThreadTurn>, System.Text.Json
    public string? CurrentContent { get; set; }       // latest assistant turn's content; null while AwaitingContext
    public string Status { get; set; } = AgentThreadStatus.AwaitingContext;
    public string LastMessageId { get; set; } = "";   // Telegram message_id of the most recent bot message in this thread
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
