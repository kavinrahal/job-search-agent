namespace JobSearch.Data;

// No per-user query filter, same reasoning as UserProfile/UserSecret: the whole point of
// this table is cross-tenant aggregation for the owner-only analytics endpoint, so a
// CurrentUserId filter would be actively wrong here, not just unnecessary.
public class AnalyticsEvent
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string EventType { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public static class AnalyticsEventType
{
    public const string Signup = "signup";
    public const string Login = "login";
    public const string CvGenerated = "cv_generated";
    public const string LetterGenerated = "letter_generated";
    public const string AnswerGenerated = "answer_generated";
    public const string Tier2Upgrade = "tier2_upgrade";
}
