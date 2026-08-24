using JobSearch.Data;

namespace JobSearch.Api.Tests;

public class ResumeIndustryTemplatesTests
{
    private static readonly string[] KnownSectionKeys =
        ["experience", "education", "skills", "projects", "credentials", "publications", "volunteering"];

    // TC01 — Every seeded industry's Junior order contains each known section key exactly once.
    // Silent failure: a missing/duplicated key here would make ResumeRenderer silently drop or
    // double-render a section for real users, not throw.
    [Fact]
    public void All_JuniorOrder_ContainsEveryKnownSectionKeyExactlyOnce()
    {
        foreach (var template in ResumeIndustryTemplates.All)
        {
            var keys = template.JuniorOrder.Select(e => e.SectionKey).ToList();
            Assert.Equal(KnownSectionKeys.OrderBy(k => k), keys.OrderBy(k => k));
            Assert.Equal(keys.Count, keys.Distinct().Count());
        }
    }

    // TC02 — Same guarantee for ExperiencedOrder.
    [Fact]
    public void All_ExperiencedOrder_ContainsEveryKnownSectionKeyExactlyOnce()
    {
        foreach (var template in ResumeIndustryTemplates.All)
        {
            var keys = template.ExperiencedOrder.Select(e => e.SectionKey).ToList();
            Assert.Equal(KnownSectionKeys.OrderBy(k => k), keys.OrderBy(k => k));
            Assert.Equal(keys.Count, keys.Distinct().Count());
        }
    }

    // TC03 — Every SectionBuilders key ResumeRenderer actually knows how to render is a subset
    // of KnownSectionKeys, so TC01/TC02 aren't asserting against a stale list of their own.
    [Fact]
    public void KnownSectionKeys_MatchesResumeRendererDefault()
    {
        var rendererKeys = ResumeRenderer.DefaultSectionConfig.Select(e => e.SectionKey).ToHashSet();
        Assert.Equal(rendererKeys, KnownSectionKeys.ToHashSet());
    }

    // TC04 — Industry keys are unique (the picker and Find() both rely on this).
    [Fact]
    public void All_IndustryKeys_AreUnique()
    {
        var keys = ResumeIndustryTemplates.All.Select(t => t.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Theory]
    [InlineData("finance_consulting")]
    [InlineData("legal")]
    [InlineData("sales_retail")]
    public void SeniorityToggleIndustries_JuniorAndExperiencedOrdersDiffer(string key)
    {
        var template = ResumeIndustryTemplates.Find(key)!;
        Assert.True(template.HasSeniorityToggle);
        Assert.NotEqual(template.JuniorOrder, template.ExperiencedOrder);
    }

    [Theory]
    [InlineData("tech")]
    [InlineData("healthcare")]
    [InlineData("trades_hospitality")]
    [InlineData("creative")]
    [InlineData("academia")]
    [InlineData("government_us")]
    public void NonToggleIndustries_AreSeniorityInvariant(string key)
    {
        var template = ResumeIndustryTemplates.Find(key)!;
        Assert.False(template.HasSeniorityToggle);
        Assert.Equal(template.JuniorOrder, template.ExperiencedOrder);
        Assert.Equal(template.OrderFor(ResumeSeniority.Junior), template.OrderFor(ResumeSeniority.Experienced));
    }

    // TC07 — Academia and government_us are an explicit scope cut (ticket: out of scope this
    // phase) that defaults to the same generic order as Tech, not a bespoke one. This guards
    // against someone silently adding a "real" order for one without updating this contract.
    [Theory]
    [InlineData("academia")]
    [InlineData("government_us")]
    public void DeferredIndustries_MatchTechBaseline(string key)
    {
        var tech = ResumeIndustryTemplates.Find("tech")!;
        var deferred = ResumeIndustryTemplates.Find(key)!;
        Assert.Equal(tech.ExperiencedOrder, deferred.ExperiencedOrder);
    }

    // TC08 — Legal's Bar Admissions block (credentials) is included in both orders, per
    // research calling it "mandatory" regardless of seniority — only its position changes.
    [Fact]
    public void Legal_CredentialsAreIncluded_InBothSeniorityOrders()
    {
        var legal = ResumeIndustryTemplates.Find("legal")!;
        Assert.True(legal.JuniorOrder.Single(e => e.SectionKey == "credentials").Included);
        Assert.True(legal.ExperiencedOrder.Single(e => e.SectionKey == "credentials").Included);
    }

    // TC09 — Legal's junior order gates Education and Credentials above Experience.
    [Fact]
    public void Legal_JuniorOrder_GatesEducationAndCredentialsAboveExperience()
    {
        var legal = ResumeIndustryTemplates.Find("legal")!;
        var keys = legal.JuniorOrder.Select(e => e.SectionKey).ToList();
        Assert.True(keys.IndexOf("education") < keys.IndexOf("experience"));
        Assert.True(keys.IndexOf("credentials") < keys.IndexOf("experience"));
    }

    // TC10 — Trades/Hospitality gates Skills then Credentials above Experience.
    [Fact]
    public void TradesHospitality_GatesSkillsThenCredentialsAboveExperience()
    {
        var trades = ResumeIndustryTemplates.Find("trades_hospitality")!;
        var keys = trades.ExperiencedOrder.Select(e => e.SectionKey).ToList();
        Assert.True(keys.IndexOf("skills") < keys.IndexOf("credentials"));
        Assert.True(keys.IndexOf("credentials") < keys.IndexOf("experience"));
    }

    // TC11 — Find() fails closed (null, not an exception or a fallback guess) for an unknown key.
    [Fact]
    public void Find_UnknownKey_ReturnsNull()
    {
        Assert.Null(ResumeIndustryTemplates.Find("not_a_real_industry"));
    }

    // TC12 — OrderFor routes to the right list by seniority for a toggle industry — the one
    // piece of logic the endpoint actually calls (Program.cs POST /resume/apply-template).
    [Fact]
    public void OrderFor_ToggleIndustry_ReturnsMatchingSeniorityOrder()
    {
        var finance = ResumeIndustryTemplates.Find("finance_consulting")!;
        Assert.Equal(finance.JuniorOrder, finance.OrderFor(ResumeSeniority.Junior));
        Assert.Equal(finance.ExperiencedOrder, finance.OrderFor(ResumeSeniority.Experienced));
    }
}
