namespace JobSearch.Data;

// 1:1 with User — background/CV base/job criteria text each agent's system prompt is
// assembled from per call, replacing the single shared context/*.{yaml,md} files. No query
// filter: always looked up by an exact known UserId (FindAsync on its own primary key),
// never via a broad list query, so there's nothing for a filter to guard here — and adding
// one would mean every lookup breaks unless CurrentUserId happens to already be set first,
// for no real safety gain over the primary-key lookup itself.
public class UserProfile
{
    public int UserId { get; set; }
    public string Background { get; set; } = "";
    public string CvBase { get; set; } = "";
    public string JobCriteria { get; set; } = "";
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
