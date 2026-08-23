using Google.Apis.Gmail.v1.Data;
using JobSearch.Data;

namespace JobSearch.Api.Tests;

public class GmailSettingsClientTests
{
    // TC01 — No existing filters at all (brand new account) never throws, just says "no".
    [Fact]
    public void HasFilterForwardingTo_NoFilters_ReturnsFalse()
    {
        Assert.False(GmailSettingsClient.HasFilterForwardingTo(null, "abc@alerts.worksanta.com"));
        Assert.False(GmailSettingsClient.HasFilterForwardingTo([], "abc@alerts.worksanta.com"));
    }

    // TC02 — Existing filters that forward somewhere else (or don't forward at all) don't
    // count as a match — this is what stops the idempotency check from producing a false
    // positive that skips creating the filter this user actually needs.
    [Fact]
    public void HasFilterForwardingTo_OtherFiltersOnly_ReturnsFalse()
    {
        var filters = new List<Filter>
        {
            new() { Action = new FilterAction { Forward = "someone-else@alerts.worksanta.com" } },
            new() { Action = new FilterAction { AddLabelIds = ["IMPORTANT"] } },
            new() { Action = null },
        };

        Assert.False(GmailSettingsClient.HasFilterForwardingTo(filters, "abc@alerts.worksanta.com"));
    }

    // TC03 — Silent failure this guards against: without this returning true, every status
    // poll would create another duplicate filter for the same address.
    [Fact]
    public void HasFilterForwardingTo_MatchingFilterExists_ReturnsTrue()
    {
        var filters = new List<Filter>
        {
            new() { Action = new FilterAction { Forward = "someone-else@alerts.worksanta.com" } },
            new() { Action = new FilterAction { Forward = "abc@alerts.worksanta.com" } },
        };

        Assert.True(GmailSettingsClient.HasFilterForwardingTo(filters, "abc@alerts.worksanta.com"));
    }

    // TC04 — The filter criteria actually covers the job boards this app already fetches
    // from directly (Seek, LinkedIn, Jora, Indeed, Adzuna) — a typo or accidental removal
    // here would silently stop forwarding from that board with no error anywhere.
    [Fact]
    public void FilterQuery_CoversKnownJobBoardSenders()
    {
        Assert.Contains("linkedin.com", GmailSettingsClient.FilterQuery);
        Assert.Contains("seek.com.au", GmailSettingsClient.FilterQuery);
        Assert.Contains("indeed.com", GmailSettingsClient.FilterQuery);
        Assert.Contains("jora.com", GmailSettingsClient.FilterQuery);
        Assert.Contains("adzuna.com.au", GmailSettingsClient.FilterQuery);
    }

    // TC05 — Unlike the single job-alert filter, there can be many per-company filters
    // forwarding to the same address, so idempotency has to key on the domain too — a
    // filter for a different company must not be mistaken for this one already existing.
    [Fact]
    public void HasCompanyFilter_FilterForDifferentCompany_ReturnsFalse()
    {
        var filters = new List<Filter>
        {
            new()
            {
                Action = new FilterAction { Forward = "abc@alerts.worksanta.com" },
                Criteria = new FilterCriteria { Query = GmailSettingsClient.CompanyFilterQuery("othercorp.com") },
            },
        };

        Assert.False(GmailSettingsClient.HasCompanyFilter(filters, "acmecorp.com", "abc@alerts.worksanta.com"));
    }

    // TC06 — The actual idempotency guard: a filter for this exact company+address already
    // exists, so EnsureCompanyFilterAsync must skip creating a duplicate.
    [Fact]
    public void HasCompanyFilter_MatchingFilterExists_ReturnsTrue()
    {
        var filters = new List<Filter>
        {
            new()
            {
                Action = new FilterAction { Forward = "abc@alerts.worksanta.com" },
                Criteria = new FilterCriteria { Query = GmailSettingsClient.CompanyFilterQuery("acmecorp.com") },
            },
        };

        Assert.True(GmailSettingsClient.HasCompanyFilter(filters, "acmecorp.com", "abc@alerts.worksanta.com"));
    }

    // TC07 — The job-alert filter and the acknowledgment filter forward to the same address
    // but carry different queries; without matching on query too, installing the second
    // filter would look like the first one "already exists" and silently never get created.
    [Fact]
    public void HasFilterForQuery_DifferentQuerySameAddress_ReturnsFalse()
    {
        var filters = new List<Filter>
        {
            new()
            {
                Action = new FilterAction { Forward = "abc@alerts.worksanta.com" },
                Criteria = new FilterCriteria { Query = GmailSettingsClient.FilterQuery },
            },
        };

        Assert.False(GmailSettingsClient.HasFilterForQuery(filters, GmailSettingsClient.AcknowledgmentFilterQuery, "abc@alerts.worksanta.com"));
    }

    // TC08 — The matching case: this exact query already installed for this address.
    [Fact]
    public void HasFilterForQuery_MatchingQueryAndAddress_ReturnsTrue()
    {
        var filters = new List<Filter>
        {
            new()
            {
                Action = new FilterAction { Forward = "abc@alerts.worksanta.com" },
                Criteria = new FilterCriteria { Query = GmailSettingsClient.AcknowledgmentFilterQuery },
            },
        };

        Assert.True(GmailSettingsClient.HasFilterForQuery(filters, GmailSettingsClient.AcknowledgmentFilterQuery, "abc@alerts.worksanta.com"));
    }

    // TC09 — A typo or accidental edit to the approved domain/phrase list should fail loudly
    // here rather than silently stop forwarding acknowledgments from a major ATS platform.
    [Fact]
    public void AcknowledgmentFilterQuery_CoversApprovedDomainsAndPhrases()
    {
        Assert.Contains("s.seek.com.au", GmailSettingsClient.AcknowledgmentFilterQuery);
        Assert.Contains("linkedin.com", GmailSettingsClient.AcknowledgmentFilterQuery);
        Assert.Contains("indeed.com", GmailSettingsClient.AcknowledgmentFilterQuery);
        Assert.Contains("myworkday.com", GmailSettingsClient.AcknowledgmentFilterQuery);
        Assert.Contains("successfully submitted", GmailSettingsClient.AcknowledgmentFilterQuery);
        Assert.Contains("thank you for applying", GmailSettingsClient.AcknowledgmentFilterQuery);
        Assert.Contains("received your application", GmailSettingsClient.AcknowledgmentFilterQuery);
    }

    // TC10 — Guards against an accidental truncation of the approved domain list (e.g. a bad
    // merge) going unnoticed — this is the set AcknowledgmentDomainCapture treats as "no
    // per-domain filter needed", so losing entries here means redundant filters get installed,
    // not a forwarding gap, but still a silent behavioral change worth catching.
    [Fact]
    public void KnownAckDomains_ContainsApprovedDomains()
    {
        Assert.Equal(20, GmailSettingsClient.KnownAckDomains.Count);
        Assert.Contains("s.seek.com.au", GmailSettingsClient.KnownAckDomains);
        Assert.Contains("ashbyhq.com", GmailSettingsClient.KnownAckDomains);
    }
}
