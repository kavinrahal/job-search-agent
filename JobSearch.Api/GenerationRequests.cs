namespace JobSearch.Api;

// Request bodies for the CV/letter/answer/edit generation endpoints.
public record GenerateRequest(int? DiscoveryId, string? PostingText);
public record AnswerRequest(string Question, int? DiscoveryId);
public record EditRequest(string Message);
