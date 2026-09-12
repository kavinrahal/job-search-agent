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

    // Set only when JobCriteria itself changes (see PUT /api/v1/profile — bumped only when
    // body.JobCriteria is not null), unlike UpdatedAt above which bumps on any profile save
    // including a Background/CvBase-only edit. GET /discoveries uses this to decide whether a
    // DiscoveredPosting's evaluation is stale: it was run against whatever JobCriteria existed
    // at evaluation time, and a criteria change since then makes that evaluation meaningless.
    // Null for any account that hasn't re-saved JobCriteria since this column was introduced —
    // treated as "no known change boundary" (nothing filtered as stale), not backfilled, so
    // existing users with stable criteria keep seeing their existing matches uninterrupted.
    public DateTime? JobCriteriaUpdatedAt { get; set; }

    // The original resume PDF, when the user's most recent intake was a file upload rather
    // than pasted text — CvBase (parsed markdown) stays the source of truth for CV tailoring
    // regardless; this is purely so the dashboard can show the real PDF instead of the
    // parsed-text approximation. Null if they've only ever pasted text.
    public byte[]? ResumePdf { get; set; }

    public User User { get; set; } = null!;
}
