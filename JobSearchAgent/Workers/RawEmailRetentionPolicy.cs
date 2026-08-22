using JobSearchAgent.Agents;
using JobSearchAgent.Models;

namespace JobSearchAgent.Workers;

// Decides which classified emails' full body text should be cleared from RawEmails once
// classification is done with them. Extracted from Program.cs so this is unit-testable —
// top-level-statement local functions can't be reached from a test project (see
// DiscoverySourceResolver for the same reasoning).
//
// The policy: keep body text only where something still reads it afterward.
// ApplicationTracker consumes classification results entirely in-memory and never re-reads
// RawEmails, so nothing needs to survive for it. The one real exception is job_alert
// digests — JobAlertProcessor re-scans stored alert emails on every run to extract job
// URLs, so those need to keep their content. Everything else — not job-related, or
// job-related but already fully acted on (an application confirmation, a rejection, an
// interview invite) — has nothing left that will ever read the body again.
public static class RawEmailRetentionPolicy
{
    public static HashSet<string> SelectMessageIdsToScrub(
        IEnumerable<(RawEmail Email, EmailClassification Classification)> results) =>
        [.. results
            .Where(r => !(r.Classification.IsJobRelated && r.Classification.Category == "job_alert"))
            .Select(r => r.Email.MessageId)];
}
