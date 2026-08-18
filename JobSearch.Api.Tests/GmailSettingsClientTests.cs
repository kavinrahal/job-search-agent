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
}
