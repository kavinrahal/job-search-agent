namespace JobSearch.Data;

// One row per Claude API call, tagged by which agent made it — precise per-agent cost
// attribution (today's Console/CSV exports only break down by model and token type, not
// by caller) and the foundation for per-user usage caps once billing needs them.
public class ClaudeUsageLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string AgentName { get; set; } = "";
    public string Model { get; set; } = "";
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public long CacheReadInputTokens { get; set; }
    public long CacheCreationInputTokens { get; set; }
    public DateTime CreatedAt { get; set; }
}

public static class ClaudeAgentName
{
    public const string CvTailorAgent = "CvTailorAgent";
    public const string CoverLetterAgent = "CoverLetterAgent";
    public const string PostingEvaluator = "PostingEvaluator";
    public const string AnswerAgent = "AnswerAgent";
    public const string EmailClassifier = "EmailClassifier";
    public const string ResumeIntakeAgent = "ResumeIntakeAgent";
    public const string ResumeBackfillAgent = "ResumeBackfillAgent";
    public const string ResumeSummaryAgent = "ResumeSummaryAgent";
    public const string PostingMatcherAgent = "PostingMatcherAgent";
    public const string CompanyExtractorAgent = "CompanyExtractorAgent";
    public const string AccuracyVerifierAgent = "AccuracyVerifierAgent";
}

// Shared sampling temperature for calls whose job is picking one of N fixed categories or
// extracting/verifying a structured fact against ground truth, rather than writing prose.
// Applied on EmailClassifier, PostingMatcherAgent, CompanyExtractorAgent, and
// AccuracyVerifierAgent — all claude-haiku-4-5. Left at the API default (1.0) on the rest:
// CvTailorAgent, CoverLetterAgent, AnswerAgent, and ResumeSummaryAgent are genuinely generative
// (phrasing variation is fine or desirable there); PostingEvaluator, ResumeBackfillAgent, and
// ResumeIntakeAgent are classification/extraction-shaped but run on claude-sonnet-5, which — like
// every model released after Claude Opus 4.6 — only accepts the default temperature of 1.0 and
// rejects any other value with a 400 (see the [Obsolete] note on MessageCreateParams.Temperature).
// There's no lever to pull for those three; sampling consistency there would need a model swap,
// which is out of scope for this pass.
public static class ClaudeTemperature
{
    public const double Classification = 0.0;
}
