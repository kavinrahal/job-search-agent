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
    // First 16 hex chars of the SHA-256 of the skill file's content as loaded by SkillLoader at
    // process startup (SkillLoader.Version) — which version of the skill-file text produced this
    // call's prompt. Null for calls made by an agent with no skill file (CompanyExtractorAgent).
    public string? SkillVersion { get; set; }
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
