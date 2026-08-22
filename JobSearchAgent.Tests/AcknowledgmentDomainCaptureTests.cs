using JobSearchAgent.Workers;

namespace JobSearchAgent.Tests;

public class AcknowledgmentDomainCaptureTests
{
    private static readonly HashSet<string> NoKnownDomains = [];

    // TC01 — Only application_confirmation results should ever trigger a new filter; a
    // rejection or interview-invite from an unrecognized sender must not install one.
    [Fact]
    public void SelectDomainsToCapture_OnlyApplicationConfirmation_ExcludesOtherCategories()
    {
        var results = new[]
        {
            Fixtures.Pair(Make.Email(fromAddress: "hr@acmecorp.com"), Make.Classification(category: "application_confirmation")),
            Fixtures.Pair(Make.Email(fromAddress: "hr@othercorp.com"), Make.Classification(category: "rejection")),
        };

        var domains = AcknowledgmentDomainCapture.SelectDomainsToCapture(results, NoKnownDomains);

        Assert.Equal(["acmecorp.com"], domains);
    }

    // TC02 — The actual point of this function: a sender already covered by the static
    // ATS/platform domain list (Part A) must not get a redundant per-domain filter.
    [Fact]
    public void SelectDomainsToCapture_AlreadyKnownDomain_Excluded()
    {
        var results = new[]
        {
            Fixtures.Pair(Make.Email(fromAddress: "noreply@s.seek.com.au"), Make.Classification(category: "application_confirmation")),
        };

        var domains = AcknowledgmentDomainCapture.SelectDomainsToCapture(results, new HashSet<string> { "s.seek.com.au" });

        Assert.Empty(domains);
    }

    // TC03 — Two acknowledgments from the same unrecognized sender in one batch shouldn't
    // produce two separate filter-install attempts for the same domain.
    [Fact]
    public void SelectDomainsToCapture_DuplicateSenderDomain_Deduplicated()
    {
        var results = new[]
        {
            Fixtures.Pair(Make.Email(messageId: "m1", fromAddress: "hr@acmecorp.com"), Make.Classification(category: "application_confirmation")),
            Fixtures.Pair(Make.Email(messageId: "m2", fromAddress: "talent@acmecorp.com"), Make.Classification(category: "application_confirmation")),
        };

        var domains = AcknowledgmentDomainCapture.SelectDomainsToCapture(results, NoKnownDomains);

        Assert.Equal(["acmecorp.com"], domains);
    }

    // TC04 — ApplicationTracker.ExtractDomain returns null for a malformed From header
    // (silent_failure risk: without filtering nulls out, this crashes when the caller later
    // uses the domain to build a Gmail filter query).
    [Fact]
    public void SelectDomainsToCapture_MalformedFromHeader_SkippedNotThrown()
    {
        var results = new[]
        {
            Fixtures.Pair(Make.Email(fromAddress: "not a valid address"), Make.Classification(category: "application_confirmation")),
        };

        var domains = AcknowledgmentDomainCapture.SelectDomainsToCapture(results, NoKnownDomains);

        Assert.Empty(domains);
    }
}
