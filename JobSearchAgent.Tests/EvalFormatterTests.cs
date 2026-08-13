using JobSearch.Data;

namespace JobSearchAgent.Tests;

public class EvalFormatterTests
{
    private static PostingEvaluation Sample(
        string recommendation = "strong_match",
        string[]? orangeFlags = null,
        string? sourceUrl = "https://au.seek.com/job/123",
        SkillMatch[]? skillMatches = null,
        string? salaryDetail = "$130k") => new()
    {
        Company             = "Canva",
        RoleTitle           = "Software Engineer",
        SourceUrl           = sourceUrl,
        Recommendation      = recommendation,
        SponsorshipVerdict  = "pass",
        LocationMatch       = "preferred",
        LocationDetail      = "Melbourne hybrid",
        ExperienceMatch     = "ideal",
        ExperienceDetail    = "3-5 years",
        SkillMatches        = skillMatches ?? [new SkillMatch("Backend stack", "strong", "C#, .NET")],
        SalaryAssessment    = "target",
        SalaryDetail        = salaryDetail,
        CompanyAssessment   = "preferred",
        RoleTypeMatch       = "preferred",
        OrangeFlags         = orangeFlags ?? [],
        Rationale           = "Good fit.",
    };

    // -------------------------------------------------------------------------
    // Format
    // -------------------------------------------------------------------------

    // TC09 — "strong_match" recommendation maps to human-readable label
    // Silent failure: a typo in the switch branch silently shows raw enum string in Telegram.
    [Fact]
    public void Format_StrongMatch_ShowsStrongMatchLabel()
    {
        var output = EvalFormatter.Format(Sample());

        Assert.Contains("STRONG MATCH", output);
    }

    // TC10 — Non-empty orange flags appear as bullets in output
    [Fact]
    public void Format_NonEmptyOrangeFlags_EachFlagAppearsInOutput()
    {
        var ev = Sample(orangeFlags: ["Salary below $100k", "Python only, no flexibility"]);

        var output = EvalFormatter.Format(ev);

        Assert.Contains("Salary below $100k", output);
        Assert.Contains("Python only, no flexibility", output);
    }

    // TC11 — Empty orange flags: no bullet character appears
    // Verifies the "none" branch without pinning the exact prose wording.
    [Fact]
    public void Format_EmptyOrangeFlags_NoBulletInOutput()
    {
        var ev = Sample(orangeFlags: []);

        var output = EvalFormatter.Format(ev);

        Assert.DoesNotContain("• ", output);
    }

    // TC12 — Non-null SourceUrl: raw URL appears in output (plain text, Telegram auto-links it)
    // Raw URL (not anchor) is required so reply_to_message.text contains it for /cv and /letter commands.
    [Fact]
    public void Format_WithSourceUrl_RawUrlInOutput()
    {
        var url = "https://au.seek.com/job/456";
        var output = EvalFormatter.Format(Sample(sourceUrl: url));

        Assert.Contains(url, output);
        Assert.DoesNotContain("<a href=", output);
    }

    // TC13 — Null SourceUrl: no URL appended
    [Fact]
    public void Format_NullSourceUrl_NoUrlInOutput()
    {
        var output = EvalFormatter.Format(Sample(sourceUrl: null));

        Assert.DoesNotContain("https://", output);
    }

    // -------------------------------------------------------------------------
    // ToPostingContext
    // -------------------------------------------------------------------------

    // TC14 — A skill match's dimension name and detail both appear in the output
    [Fact]
    public void ToPostingContext_NonEmptySkillMatches_DimensionAndDetailAppear()
    {
        var ev = Sample(skillMatches: [new SkillMatch("Backend stack", "strong", "C#, .NET, Azure")]);

        var output = EvalFormatter.ToPostingContext(ev);

        Assert.Contains("Backend stack: C#, .NET, Azure", output);
    }

    // TC15 — Empty skill matches array shows fallback "not stated"
    [Fact]
    public void ToPostingContext_EmptySkillMatches_ShowsNotStated()
    {
        var ev = Sample(skillMatches: []);

        var output = EvalFormatter.ToPostingContext(ev);

        Assert.Contains("not stated", output);
    }

    // TC16 — Null SalaryDetail shows fallback "not stated"
    // Silent failure: omitting the null-guard renders literal null in the context string sent to Claude.
    [Fact]
    public void ToPostingContext_NullSalaryDetail_ShowsNotStated()
    {
        var ev = Sample(salaryDetail: null);

        var output = EvalFormatter.ToPostingContext(ev);

        Assert.Contains("not stated", output);
    }

    // TC17 — Null SourceUrl shows fallback "not available"
    [Fact]
    public void ToPostingContext_NullSourceUrl_ShowsNotAvailable()
    {
        var ev = Sample(sourceUrl: null);

        var output = EvalFormatter.ToPostingContext(ev);

        Assert.Contains("not available", output);
    }

    // TC18 — Company and RoleTitle always appear (core identity fields)
    [Fact]
    public void ToPostingContext_AlwaysIncludesCompanyAndRole()
    {
        var output = EvalFormatter.ToPostingContext(Sample());

        Assert.Contains("Canva", output);
        Assert.Contains("Software Engineer", output);
    }
}
