using JobSearch.Data;

namespace JobSearchAgent.Tests;

public class TargetJobTitlesTests
{
    // TC01 — Plain unquoted scalar, as js-yaml emits for a comma value with no special chars.
    [Fact]
    public void Parse_UnquotedValue_SplitsOnComma()
    {
        var result = TargetJobTitles.Parse("target_job_titles: Sous Chef, Line Cook, Kitchen Manager\nother_key: value");

        Assert.Equal(["Sous Chef", "Line Cook", "Kitchen Manager"], result);
    }

    // TC02 — Quoted scalar (js-yaml quotes when the value needs it) — quotes stripped, not
    // left in as part of the first/last title.
    [Fact]
    public void Parse_DoubleQuotedValue_QuotesStripped()
    {
        var result = TargetJobTitles.Parse("target_job_titles: \"Software Engineer, Backend Developer\"");

        Assert.Equal(["Software Engineer", "Backend Developer"], result);
    }

    // TC03 — Key absent entirely (old criteria saved before this field existed, or hand-edited
    // YAML that dropped it) — empty, not a throw.
    [Fact]
    public void Parse_KeyMissing_ReturnsEmpty()
    {
        var result = TargetJobTitles.Parse("employment_type_preference:\n  - full_time");

        Assert.Empty(result);
    }

    // TC04 — Key present but blank — the "user opened the field and left it empty" case,
    // must come back empty so the caller correctly treats this as "not filled in".
    [Fact]
    public void Parse_EmptyValue_ReturnsEmpty()
    {
        var result = TargetJobTitles.Parse("target_job_titles: \"\"\nother_key: value");

        Assert.Empty(result);
    }

    // TC05 — Null/empty input criteria (blank profile) — empty, not a throw.
    [Fact]
    public void Parse_NullOrEmptyCriteria_ReturnsEmpty()
    {
        Assert.Empty(TargetJobTitles.Parse(null));
        Assert.Empty(TargetJobTitles.Parse(""));
    }

    // TC06 — Whitespace around individual titles is trimmed.
    [Fact]
    public void Parse_ExtraWhitespace_Trimmed()
    {
        var result = TargetJobTitles.Parse("target_job_titles:   Sous Chef ,  Line Cook  ");

        Assert.Equal(["Sous Chef", "Line Cook"], result);
    }
}
