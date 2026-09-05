namespace JobSearch.Data;

public class DiscoveredPosting
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Url { get; set; } = "";
    public string Source { get; set; } = "";       // "seek"
    public string Title { get; set; } = "";
    public string Company { get; set; } = "";
    public string? Recommendation { get; set; }    // null until evaluated; "error" on fetch/eval failure
    public string? EvaluationJson { get; set; }    // full serialised PostingEvaluation
    // The posting text the evaluation was actually run against — a full page fetch where that
    // worked, otherwise the feed's own description. Cached here because generating a CV or
    // cover letter later needs the same text, and re-fetching at that point fails outright on
    // Cloudflare-protected boards (Seek in particular). Null on rows discovered before this
    // column existed; those still fall back to a live fetch and then EvaluationJson.
    public string? PostingText { get; set; }
    public string? DisqualifierHit { get; set; }
    public DateTime DiscoveredAt { get; set; }
    public DateTime? EvaluatedAt { get; set; }
    public bool EmailNotificationSent { get; set; }
    // Evaluation failures (fetch error, Claude error, etc.) recorded against this posting,
    // reset to 0 on a successful evaluation. Once it reaches a worker's dead-letter cap (see
    // MaxFailureCount in JobDiscoveryWorker/JobAlertProcessor), the posting is excluded from
    // all future retries instead of being re-fetched and re-evaluated forever.
    public int FailureCount { get; set; }
}
