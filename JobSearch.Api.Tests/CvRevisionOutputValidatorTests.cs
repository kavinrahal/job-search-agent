using JobSearch.Data;

namespace JobSearch.Api.Tests;

// Tests the guard the CV branch of /threads/{id}/edit (Program.cs) runs on CvTailorAgent.
// ReviseAsync's output before ever persisting it as the thread's CurrentContent. Mirrors
// CoverLetterOutputValidatorTests/PostingTextSufficiencyTests' approach — this is a pure
// heuristic function tested directly, without a live Claude call, per architecture-conventions.md's
// testing pattern.
public class CvRevisionOutputValidatorTests
{
    private static string RealisticResume() =>
        "# Jordan Rivers\n\njordan@example.com | 0400 000 000 | Sydney, NSW\n\n" +
        "## Summary\n\n" +
        string.Join(" ", Enumerable.Repeat("Software engineer with commercial experience across a modern stack.", 6)) + "\n\n" +
        "## Experience\n\n" +
        "### Software Engineer – Acme Corp\nSydney | Jan 2023 – Present\n\n" +
        "- Built and shipped a customer-facing feature end to end.\n" +
        "- Reduced page load time by 40 percent through targeted caching.\n";

    // TC01 — A refusal shaped like the real cover-letter incident (same root cause, applied to a
    // CV revision instead): no "# " heading, just the model narrating its own reasoning.
    [Fact]
    public void LooksLikeRevisedResume_RefusalText_ReturnsFalse()
    {
        var refusal = "I notice the background file I've been given is essentially empty. " +
            "I can't produce a credible, specific revision without that content. " +
            "I don't know whose resume this actually is.";

        Assert.False(CvRevisionOutputValidator.LooksLikeRevisedResume(refusal));
    }

    // TC02 — The degenerate one-word-response case (mirrors the historical CoverLetterAgent
    // incident referenced in its own comment).
    [Fact]
    public void LooksLikeRevisedResume_SingleWordResponse_ReturnsFalse()
    {
        Assert.False(CvRevisionOutputValidator.LooksLikeRevisedResume("Sure"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\t  ")]
    public void LooksLikeRevisedResume_NullOrBlank_ReturnsFalse(string? text)
    {
        Assert.False(CvRevisionOutputValidator.LooksLikeRevisedResume(text));
    }

    // TC03 — Prose that never opens with the "# {Name}" heading every ResumeRenderer.Render
    // output has, even if it's long enough to otherwise look plausible.
    [Fact]
    public void LooksLikeRevisedResume_MissingNameHeading_ReturnsFalse()
    {
        var text = "Here is the revised resume with your requested changes applied throughout. " +
            string.Join(" ", Enumerable.Repeat("This section describes relevant experience.", 10));

        Assert.False(CvRevisionOutputValidator.LooksLikeRevisedResume(text));
    }

    // TC04 — A refusal that happens to open with a valid "# " heading (e.g. it echoes the
    // candidate's name before explaining itself) is still caught by the meta-commentary signal
    // check, not just the heading/length checks alone.
    [Fact]
    public void LooksLikeRevisedResume_RefusalWithHeadingAndLength_StillReturnsFalse()
    {
        var text = "# Jordan Rivers\n\n" + string.Join(" ", Enumerable.Repeat(
            "I notice the background file does not contain enough detail to revise this resume.", 6));

        Assert.False(CvRevisionOutputValidator.LooksLikeRevisedResume(text));
    }

    [Fact]
    public void LooksLikeRevisedResume_RealisticResume_ReturnsTrue()
    {
        Assert.True(CvRevisionOutputValidator.LooksLikeRevisedResume(RealisticResume()));
    }

    // TC05 — A short but genuine resume for a candidate with a sparse background (one role, no
    // summary/education/projects) still passes — the floor exists for degenerate responses, not
    // to police length the way the cover letter's word-count rule does.
    [Fact]
    public void LooksLikeRevisedResume_SparseButGenuineResume_ReturnsTrue()
    {
        var text = "# Alex Chen\n\nalex@example.com | Melbourne, VIC\n\n" +
            "## Experience\n\n### Support Engineer – Acme Corp\nMelbourne | Jan 2025 – Present\n\n" +
            "- Resolved customer tickets and escalations across a shared queue.\n";

        Assert.True(CvRevisionOutputValidator.LooksLikeRevisedResume(text));
    }

    // TC06 — Word count boundary: just under the floor is rejected.
    [Fact]
    public void LooksLikeRevisedResume_TooShort_ReturnsFalse()
    {
        var text = "# Jordan Rivers\n\n" + string.Join(" ", Enumerable.Repeat("word", CvRevisionOutputValidator.MinWords - 5));

        Assert.False(CvRevisionOutputValidator.LooksLikeRevisedResume(text));
    }
}
