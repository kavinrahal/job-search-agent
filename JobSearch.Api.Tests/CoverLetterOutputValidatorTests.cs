using JobSearch.Data;

namespace JobSearch.Api.Tests;

// Tests the guard GenerateArtifactAsync (Program.cs) and the /threads/{id}/edit revision branch
// run on CoverLetterAgent's output before ever persisting it as a Complete thread or leaving the
// credit spent. Mirrors PostingTextSufficiencyTests' approach — this is a pure heuristic function
// tested directly, without a live Claude call, per architecture-conventions.md's testing pattern.
public class CoverLetterOutputValidatorTests
{
    private static string RealisticLetter(string salutation = "Dear Hiring Manager,") =>
        salutation + " " + string.Join(" ", Enumerable.Repeat(
            "I'm a software engineer with several years of commercial experience across a modern stack.",
            30)); // ~360 words, well inside the skill's 350-500 word rule

    // TC01 — The real incident: the model's own refusal reasoning, verbatim, with no salutation.
    [Fact]
    public void LooksLikeCoverLetter_RealIncidentRefusalText_ReturnsFalse()
    {
        var refusal = "I notice the background file I've been given is essentially empty. " +
            "I can't write a credible, specific cover letter without that content. " +
            "The candidate name in the background file is 'Staging Test Two', but the skill's " +
            "sign-off instruction hardcodes 'Kavin Abeysinghe'. These don't match, so I don't know " +
            "whose letter this actually is.";

        Assert.False(CoverLetterOutputValidator.LooksLikeCoverLetter(refusal));
    }

    // TC02 — The historical degenerate case referenced in CoverLetterAgent.cs's own comment: the
    // entire generated content was the single word "To".
    [Fact]
    public void LooksLikeCoverLetter_SingleWordResponse_ReturnsFalse()
    {
        Assert.False(CoverLetterOutputValidator.LooksLikeCoverLetter("To"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\t  ")]
    public void LooksLikeCoverLetter_NullOrBlank_ReturnsFalse(string? text)
    {
        Assert.False(CoverLetterOutputValidator.LooksLikeCoverLetter(text));
    }

    // TC03 — No salutation at all (e.g. the model dropped straight into prose) is rejected even
    // when the rest of the shape (length) looks plausible.
    [Fact]
    public void LooksLikeCoverLetter_MissingSalutation_ReturnsFalse()
    {
        var text = "I'm a software engineer applying for this role. " +
            string.Join(" ", Enumerable.Repeat("I have relevant experience in this field.", 30));

        Assert.False(CoverLetterOutputValidator.LooksLikeCoverLetter(text));
    }

    // TC04 — A refusal that happens to open with a valid salutation shape and pad itself out to a
    // plausible length is still caught by the meta-commentary signal check, not just the
    // salutation/length checks alone.
    [Fact]
    public void LooksLikeCoverLetter_RefusalWithSalutationAndLength_StillReturnsFalse()
    {
        var text = "Dear Hiring Manager, " + string.Join(" ", Enumerable.Repeat(
            "I notice the background file I have been given does not contain enough detail to proceed.",
            20));

        Assert.False(CoverLetterOutputValidator.LooksLikeCoverLetter(text));
    }

    [Theory]
    [InlineData("Dear Hiring Manager,")]
    [InlineData("Dear Jane Smith,")]
    [InlineData("To the Hiring Manager,")]
    public void LooksLikeCoverLetter_RealisticLetter_ReturnsTrue(string salutation)
    {
        Assert.True(CoverLetterOutputValidator.LooksLikeCoverLetter(RealisticLetter(salutation)));
    }

    // TC05 — Word count boundaries: just under the floor is rejected, just over the ceiling is
    // rejected, and a realistic mid-range letter (already covered above) passes.
    [Fact]
    public void LooksLikeCoverLetter_TooShort_ReturnsFalse()
    {
        var text = "Dear Hiring Manager, " + string.Join(" ", Enumerable.Repeat("word", CoverLetterOutputValidator.MinWords - 5));

        Assert.False(CoverLetterOutputValidator.LooksLikeCoverLetter(text));
    }

    [Fact]
    public void LooksLikeCoverLetter_TooLong_ReturnsFalse()
    {
        var text = "Dear Hiring Manager, " + string.Join(" ", Enumerable.Repeat("word", CoverLetterOutputValidator.MaxWords + 50));

        Assert.False(CoverLetterOutputValidator.LooksLikeCoverLetter(text));
    }

    // TC06 — Case-insensitivity: an uppercase salutation and mixed-case refusal signal are still
    // caught correctly.
    [Fact]
    public void LooksLikeCoverLetter_CaseInsensitive()
    {
        var validUpper = "DEAR HIRING MANAGER, " + string.Join(" ", Enumerable.Repeat("I have relevant commercial experience for this position.", 25));
        Assert.True(CoverLetterOutputValidator.LooksLikeCoverLetter(validUpper));

        var refusalMixedCase = "Dear Hiring Manager, " + string.Join(" ", Enumerable.Repeat(
            "I Notice The background file lacks sufficient detail to continue writing this letter.", 20));
        Assert.False(CoverLetterOutputValidator.LooksLikeCoverLetter(refusalMixedCase));
    }
}
