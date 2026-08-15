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

    // TC02 — HTML entities decoded (&amp; → &, &lt; → <)
    // Silent failure: undecoded entities like "&amp;" reach Claude as literal text, polluting the job description.
    [Fact]
    public void StripHtml_HtmlEntities_Decoded()
    {
        var result = JobFetcherUtils.StripHtml("Salary &amp; benefits &lt;negotiable&gt;");

        Assert.Contains("Salary & benefits", result);
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
}
