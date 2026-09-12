using JobSearch.Data;

namespace JobSearchAgent.Tests;

public class JobFetcherUtilsTests
{
    // =========================================================================
    // StripHtml
    // =========================================================================

    // TC01 — HTML tags removed, text retained
    [Fact]
    public void StripHtml_HtmlTags_Removed()
    {
        var result = JobFetcherUtils.StripHtml("<p>Hello <b>world</b></p>");

        Assert.Contains("Hello", result);
        Assert.Contains("world", result);
        Assert.DoesNotContain("<", result);
    }

    // TC02 — HTML entities decoded (&amp; → &, &quot; → ")
    // Silent failure: undecoded entities like "&amp;" reach Claude as literal text, polluting the job description.
    [Fact]
    public void StripHtml_HtmlEntities_Decoded()
    {
        var result = JobFetcherUtils.StripHtml("Salary &amp; benefits: &quot;negotiable&quot;");

        Assert.Contains("Salary & benefits", result);
        Assert.Contains("\"negotiable\"", result);
    }

    // TC02b — Double-HTML-encoded content (confirmed live on Greenhouse: the API's `content`
    // field is itself entity-escaped markup, e.g. a literal "&lt;div&gt;" string rather than a
    // real "<div>" tag) has its tags actually removed, not merely decoded into visible tag text.
    // Silent failure: decoding after stripping (the old order) finds no literal "<"/">" to strip,
    // then reveals the tags too late — they leak into the evaluator's input as "<div>Some text</div>".
    [Fact]
    public void StripHtml_DoubleEncodedHtml_TagsRemovedNotJustRevealed()
    {
        var result = JobFetcherUtils.StripHtml("&lt;div class=&quot;role&quot;&gt;&lt;p&gt;Sponsors 482 visas.&lt;/p&gt;&lt;/div&gt;");

        Assert.DoesNotContain("<", result);
        Assert.DoesNotContain("&lt;", result);
        Assert.Contains("Sponsors 482 visas.", result);
    }

    // TC03 — Multiple consecutive spaces collapsed to single space
    [Fact]
    public void StripHtml_ExcessiveWhitespace_Collapsed()
    {
        var result = JobFetcherUtils.StripHtml("a    b    c");

        Assert.Equal("a b c", result);
    }

    // TC04 — Empty string input returns empty string (no exception)
    [Fact]
    public void StripHtml_EmptyString_ReturnsEmpty()
    {
        var result = JobFetcherUtils.StripHtml("");

        Assert.Equal("", result);
    }

    // =========================================================================
    // IsAuLocation
    // =========================================================================

    // TC05 — Null location → true (globally remote or unspecified, include it)
    // Silent failure: returning false for null would exclude all Greenhouse/Lever jobs with no location set.
    [Fact]
    public void IsAuLocation_Null_ReturnsTrue()
    {
        Assert.True(JobFetcherUtils.IsAuLocation(null));
    }

    // TC06 — Empty/whitespace location → true (same as null)
    [Fact]
    public void IsAuLocation_Empty_ReturnsTrue()
    {
        Assert.True(JobFetcherUtils.IsAuLocation("   "));
    }

    // TC07 — "Melbourne, VIC" → true
    [Fact]
    public void IsAuLocation_Melbourne_ReturnsTrue()
    {
        Assert.True(JobFetcherUtils.IsAuLocation("Melbourne, VIC"));
    }

    // TC08 — "Sydney, NSW, Australia" → true (contains "australia" token)
    [Fact]
    public void IsAuLocation_Australia_ReturnsTrue()
    {
        Assert.True(JobFetcherUtils.IsAuLocation("Sydney, NSW, Australia"));
    }

    // TC09 — "Remote" → true (remote positions are always considered)
    [Fact]
    public void IsAuLocation_Remote_ReturnsTrue()
    {
        Assert.True(JobFetcherUtils.IsAuLocation("Remote"));
    }

    // TC10 — "London, UK" → false
    // Silent failure: returning true includes overseas jobs, wastes Claude eval quota and pollutes results.
    [Fact]
    public void IsAuLocation_London_ReturnsFalse()
    {
        Assert.False(JobFetcherUtils.IsAuLocation("London, UK"));
    }

    // TC11 — "San Francisco, CA" → false
    [Fact]
    public void IsAuLocation_SanFrancisco_ReturnsFalse()
    {
        Assert.False(JobFetcherUtils.IsAuLocation("San Francisco, CA"));
    }

    // TC17 — "AU - HQ - NSW" → true. Real Greenhouse location string (Eucalyptus) that was
    // silently dropped before AU/NSW coverage was added: contains none of the original
    // melbourne/vic/victoria/australia/remote/hybrid tokens despite being a genuine AU posting.
    [Fact]
    public void IsAuLocation_AuDashNsw_ReturnsTrue()
    {
        Assert.True(JobFetcherUtils.IsAuLocation("AU - HQ - NSW"));
    }

    // TC18 — "Sydney, Australia" → true
    [Fact]
    public void IsAuLocation_SydneyAustralia_ReturnsTrue()
    {
        Assert.True(JobFetcherUtils.IsAuLocation("Sydney, Australia"));
    }

    // TC19 — "Remote (QLD)" → true
    [Fact]
    public void IsAuLocation_RemoteQld_ReturnsTrue()
    {
        Assert.True(JobFetcherUtils.IsAuLocation("Remote (QLD)"));
    }

    // TC20 — each newly-added state/territory abbreviation matches as a realistic location string.
    [Theory]
    [InlineData("Perth, WA")]
    [InlineData("Adelaide, SA")]
    [InlineData("Hobart, TAS")]
    [InlineData("Canberra, ACT")]
    [InlineData("Darwin, NT")]
    [InlineData("Brisbane, QLD")]
    public void IsAuLocation_StateAbbreviations_ReturnTrue(string location)
    {
        Assert.True(JobFetcherUtils.IsAuLocation(location));
    }

    // TC21 — each newly-added full state/territory name matches.
    [Theory]
    [InlineData("Sydney, New South Wales")]
    [InlineData("Brisbane, Queensland")]
    [InlineData("Perth, Western Australia")]
    [InlineData("Adelaide, South Australia")]
    [InlineData("Hobart, Tasmania")]
    [InlineData("Canberra, Australian Capital Territory")]
    [InlineData("Darwin, Northern Territory")]
    public void IsAuLocation_FullStateNames_ReturnTrue(string location)
    {
        Assert.True(JobFetcherUtils.IsAuLocation(location));
    }

    // TC22 — each newly-added major city name matches on its own (no state suffix needed).
    [Theory]
    [InlineData("Sydney")]
    [InlineData("Brisbane")]
    [InlineData("Perth")]
    [InlineData("Adelaide")]
    [InlineData("Canberra")]
    [InlineData("Darwin")]
    [InlineData("Hobart")]
    public void IsAuLocation_CityNames_ReturnTrue(string location)
    {
        Assert.True(JobFetcherUtils.IsAuLocation(location));
    }

    // TC23 — word-boundary fix: "vic" must NOT match as a bare substring inside an unrelated
    // word. Silent failure (pre-fix): "Customer Service, London" would incorrectly be treated
    // as AU-relevant because "service" contains "vic" as a raw substring.
    [Fact]
    public void IsAuLocation_VicSubstringInService_DoesNotMatch()
    {
        Assert.False(JobFetcherUtils.IsAuLocation("Customer Service, London"));
    }

    // TC24 — word-boundary fix, second example: "au" must not match inside an unrelated word.
    [Fact]
    public void IsAuLocation_AuSubstringInSaudi_DoesNotMatch()
    {
        Assert.False(JobFetcherUtils.IsAuLocation("Riyadh, Saudi Arabia"));
    }

    // TC25 — "VIC" as a genuine standalone token (all-caps, as commonly written) still matches
    // after the word-boundary fix — confirms the fix didn't regress the real case it must catch.
    [Fact]
    public void IsAuLocation_VicStandaloneToken_StillMatches()
    {
        Assert.True(JobFetcherUtils.IsAuLocation("Geelong, VIC"));
    }

    // =========================================================================
    // RankByCompany
    // =========================================================================

    private static JobFeedItem Item(string company, string url) => new() { Company = company, Url = url };

    // TC12 — No company given → candidates returned in original order, untouched.
    [Fact]
    public void RankByCompany_NoCompany_OrderUnchanged()
    {
        var items = new List<JobFeedItem> { Item("Acme", "a"), Item("Codafication", "b") };

        var result = JobFetcherUtils.RankByCompany(items, null);

        Assert.Equal(["a", "b"], result.Select(i => i.Url));
    }

    // TC13 — A matching company is moved to the front; this is the exact bug reported: a real
    // Codafication listing ranked outside the visible results when a plain title search alone
    // couldn't distinguish it from unrelated "software engineer" postings.
    [Fact]
    public void RankByCompany_MatchPresent_MovesMatchFirst()
    {
        var items = new List<JobFeedItem> { Item("Acme", "a"), Item("Codafication", "b"), Item("Beta Corp", "c") };

        var result = JobFetcherUtils.RankByCompany(items, "Codafication");

        Assert.Equal("b", result[0].Url);
    }

    // TC14 — No candidate matches the given company → original relative order preserved
    // (a stable no-op, not an error or empty result).
    [Fact]
    public void RankByCompany_NoMatch_OrderUnchanged()
    {
        var items = new List<JobFeedItem> { Item("Acme", "a"), Item("Beta Corp", "b") };

        var result = JobFetcherUtils.RankByCompany(items, "Codafication");

        Assert.Equal(["a", "b"], result.Select(i => i.Url));
    }

    // TC15 — Match is case-insensitive.
    [Fact]
    public void RankByCompany_DifferentCase_StillMatches()
    {
        var items = new List<JobFeedItem> { Item("Acme", "a"), Item("CODAFICATION", "b") };

        var result = JobFetcherUtils.RankByCompany(items, "codafication");

        Assert.Equal("b", result[0].Url);
    }

    // TC16 — Matches in either containment direction, so "Codafication Pty Ltd" typed by the
    // user still matches a candidate whose Company field is just "Codafication".
    [Fact]
    public void RankByCompany_PartialNameEitherDirection_Matches()
    {
        var items = new List<JobFeedItem> { Item("Acme", "a"), Item("Codafication", "b") };

        var result = JobFetcherUtils.RankByCompany(items, "Codafication Pty Ltd");

        Assert.Equal("b", result[0].Url);
    }
}
