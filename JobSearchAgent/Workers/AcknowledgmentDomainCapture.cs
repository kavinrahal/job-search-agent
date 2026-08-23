using JobSearch.Data;
using JobSearchAgent.Agents;
using JobSearchAgent.Models;

namespace JobSearchAgent.Workers;

// Filter tracking mode's phrase filter (GmailSettingsClient.AcknowledgmentFilterQuery) catches
// direct company/agency senders that don't share a domain with any known ATS platform. Once one
// of those has actually been classified as a real acknowledgment, its domain is worth a
// dedicated per-domain filter from then on — cheaper for Gmail to match and no longer dependent
// on that sender's exact phrasing staying consistent. Extracted for the same reason
// RawEmailRetentionPolicy is: pure decision logic, testable without a real Gmail SDK call.
public static class AcknowledgmentDomainCapture
{
    public static IReadOnlyList<string> SelectDomainsToCapture(
        IEnumerable<(RawEmail Email, EmailClassification Classification)> results,
        IReadOnlySet<string> alreadyKnownDomains) =>
        [.. results
            .Where(r => r.Classification.Category == "application_confirmation")
            .Select(r => ApplicationTracker.ExtractDomain(r.Email.FromAddress))
            .Where(d => d is not null && !alreadyKnownDomains.Contains(d))
            .Select(d => d!)
            .Distinct()];
}
