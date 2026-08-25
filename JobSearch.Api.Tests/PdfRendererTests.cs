using JobSearch.Api.Services;
using JobSearch.Data;

namespace JobSearch.Api.Tests;

public class PdfRendererTests
{
    // TC01 — an empty-text bullet (as ResumeRenderer.RenderBulletList emits for a
    // cleared-to-empty achievement: "- " with nothing after) must not leak into the output.
    // Regression test for the same trim-before-prefix-check bug fixed in renderResumeMarkdown.ts
    // (PR #47): AddLine used to check "- "/"* " against a line already TrimEnd()'d, so "- "
    // became "-", failed the prefix match, and fell through to the plain-text branch, printing a
    // stray "-" line. Unlike the preview's <ul>-grouped list, bullets here are independent Row
    // items, so the bug didn't drop the surrounding bullets — it only left the stray "-" behind.
    // Both are asserted: the surrounding bullets to guard the architecture assumption, the
    // missing "-" to catch the actual defect.
    [Fact]
    public void RenderCv_EmptyBulletMidList_SurroundingBulletsStillRender()
    {
        var markdown = "## Experience\n- First real bullet\n- \n- Third real bullet after the empty one";

        var bytes = PdfRenderer.RenderCv(markdown);
        var text = PdfTextExtractor.ExtractText(bytes);

        Assert.Contains("First real bullet", text);
        Assert.Contains("Third real bullet after the empty one", text);
        // The empty bullet has no text of its own in this markdown, so the only way a "-"
        // character can appear in the extracted text is if it leaked through as an unrendered
        // literal bullet marker instead of being skipped.
        Assert.DoesNotContain("-", text);
    }
}
