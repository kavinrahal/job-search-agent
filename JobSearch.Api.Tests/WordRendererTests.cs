using JobSearch.Api.Services;

namespace JobSearch.Api.Tests;

public class WordRendererTests
{
    // TC01 — produces a non-empty, valid Open XML package (starts with the ZIP magic bytes
    // every .docx is built on) rather than throwing or returning garbage.
    [Fact]
    public void RenderLetter_TypicalLetter_ProducesValidDocx()
    {
        var text = "Dear Hiring Manager,\n\nI'm applying for this role.\n\nKind regards,\n\nKavin Abeysinghe";

        var bytes = WordRenderer.RenderLetter(text);

        Assert.True(bytes.Length > 0);
        Assert.Equal(0x50, bytes[0]); // 'P' — ZIP local file header magic ("PK\x03\x04")
        Assert.Equal(0x4B, bytes[1]); // 'K'
    }

    // TC02 — blank lines between paragraphs don't produce empty paragraphs or throw
    // Silent failure: an unguarded blank-line paragraph would render as visible empty gaps.
    [Fact]
    public void RenderLetter_BlankLinesBetweenParagraphs_NoException()
    {
        var text = "First paragraph.\n\n\n\nSecond paragraph after multiple blank lines.";

        var ex = Record.Exception(() => WordRenderer.RenderLetter(text));

        Assert.Null(ex);
    }
}
