namespace JobSearchAgent.Models;

public record RawEmail(
    string MessageId,
    string ThreadId,
    string FromAddress,
    string Subject,
    string BodyText,
    DateTimeOffset ReceivedAt
);
