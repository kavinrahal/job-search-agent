using System.Text;
using JobSearch.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace JobSearchAgent.Tests;

public class PdfTextExtractorTests
{
    static PdfTextExtractorTests() => QuestPDF.Settings.License = LicenseType.Community;

    private static byte[] BuildPdf(string? bodyText) =>
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                if (bodyText is not null)
                    page.Content().Text(bodyText);
            });
        }).GeneratePdf();

    // TC01 — The actual point of this class: a real PDF's text comes back out, so
    // ResumeIntakeAgent can send it to Claude as plain text instead of the raw PDF.
    [Fact]
    public void ExtractText_RealPdfWithText_ReturnsThatText()
    {
        var pdf = BuildPdf("Kavin Abeysinghe, Software Engineer");

        var text = PdfTextExtractor.ExtractText(pdf);

        Assert.Contains("Kavin Abeysinghe", text);
    }

    // TC02 — Multi-page PDFs must not silently lose every page but the first.
    [Fact]
    public void ExtractText_MultiPagePdf_ReturnsTextFromAllPages()
    {
        var pdf = Document.Create(container =>
        {
            container.Page(page => { page.Size(PageSizes.A4); page.Content().Text("Page one marker"); });
            container.Page(page => { page.Size(PageSizes.A4); page.Content().Text("Page two marker"); });
        }).GeneratePdf();

        var text = PdfTextExtractor.ExtractText(pdf);

        Assert.Contains("Page one marker", text);
        Assert.Contains("Page two marker", text);
    }

    // TC03 — Silent-failure risk this guards against: a scanned/image-only PDF (no text
    // layer, PdfPig does not OCR) must fail loudly, not silently send an empty resume to
    // Claude and produce a confusing "your resume is blank" result.
    [Fact]
    public void ExtractText_NoTextLayer_ThrowsPdfTextExtractionException()
    {
        var pdf = BuildPdf(null);

        Assert.Throws<PdfTextExtractionException>(() => PdfTextExtractor.ExtractText(pdf));
    }

    // TC04 — Corrupt/non-PDF bytes (e.g. a mislabeled upload) must produce the same typed
    // exception as the "no text" case, not an unhandled PdfPig-internal exception type the
    // API layer doesn't know to catch.
    [Fact]
    public void ExtractText_NotAPdf_ThrowsPdfTextExtractionException()
    {
        var garbage = Encoding.UTF8.GetBytes("this is not a pdf file");

        Assert.Throws<PdfTextExtractionException>(() => PdfTextExtractor.ExtractText(garbage));
    }

    // TC05 — Content-sniffing, not extension/Content-Type trust: bytes that don't start with
    // the PDF magic number are rejected immediately with a specific message, not left to
    // whatever PdfPig happens to throw for them.
    [Fact]
    public void HasPdfMagicBytes_WrongMagicBytes_ReturnsFalse()
    {
        var fakePdf = Encoding.UTF8.GetBytes("PK\x03\x04not actually a pdf, just renamed");

        Assert.False(PdfTextExtractor.HasPdfMagicBytes(fakePdf));
        var ex = Assert.Throws<PdfTextExtractionException>(() => PdfTextExtractor.ExtractText(fakePdf));
        Assert.Contains("doesn't look like a PDF", ex.Message);
    }

    // TC06 — A real PDF's magic bytes pass the sniff.
    [Fact]
    public void HasPdfMagicBytes_RealPdf_ReturnsTrue()
    {
        var pdf = BuildPdf("some text");

        Assert.True(PdfTextExtractor.HasPdfMagicBytes(pdf));
    }

    // TC07 — Size cap enforced server-side regardless of what any client-side check did (or
    // didn't) do. One byte over MaxPdfBytes must be rejected before ever reaching PdfPig.
    [Fact]
    public void ExtractText_OverSizeLimit_ThrowsPdfTextExtractionException()
    {
        var oversized = new byte[PdfTextExtractor.MaxPdfBytes + 1];
        "%PDF-"u8.ToArray().CopyTo(oversized, 0);

        var ex = Assert.Throws<PdfTextExtractionException>(() => PdfTextExtractor.ExtractText(oversized));
        Assert.Contains("too large", ex.Message);
    }

    // TC08 — Empty upload rejected with a clear message instead of PdfPig's own exception for
    // a zero-length document.
    [Fact]
    public void ExtractText_EmptyFile_ThrowsPdfTextExtractionException()
    {
        Assert.Throws<PdfTextExtractionException>(() => PdfTextExtractor.ExtractText([]));
    }

    // TC09 — Decompression-bomb-style proxy: a PDF that's technically well-formed but has far
    // more pages than any resume needs must be rejected rather than let the parser (and the
    // rest of the pipeline downstream) churn through all of them.
    [Fact]
    public void ExtractText_TooManyPages_ThrowsPdfTextExtractionException()
    {
        var pdf = Document.Create(container =>
        {
            for (var i = 0; i < 51; i++)
            {
                var pageNum = i;
                container.Page(page => { page.Size(PageSizes.A4); page.Content().Text($"Page {pageNum}"); });
            }
        }).GeneratePdf();

        var ex = Assert.Throws<PdfTextExtractionException>(() => PdfTextExtractor.ExtractText(pdf));
        Assert.Contains("pages", ex.Message);
    }

    // TC10 — Polyglot-style proxy: bytes that pass the magic-byte sniff (start with "%PDF-")
    // but aren't a real, well-formed PDF structure underneath must still be rejected by the
    // parse step, not accepted just because the header looked right.
    [Fact]
    public void ExtractText_ValidMagicBytesButNotARealPdf_ThrowsPdfTextExtractionException()
    {
        var polyglotish = Encoding.UTF8.GetBytes("%PDF-1.4\nthis is not a real pdf body, just a header stapled onto garbage");

        var ex = Assert.Throws<PdfTextExtractionException>(() => PdfTextExtractor.ExtractText(polyglotish));
        Assert.Contains("corrupted", ex.Message);
    }

    // TC11 — A hung/adversarially slow parse must not hang the request: the async path is
    // bounded by a timeout, and a timeout is surfaced as the same typed exception the caller
    // already knows how to turn into a clean 400. Uses the internal timeout-parameterized
    // overload with a near-zero timeout so this test doesn't itself take 10 seconds.
    [Fact]
    public async Task ExtractTextAsync_TimesOut_ThrowsPdfTextExtractionException()
    {
        var pdf = BuildPdf("Kavin Abeysinghe, Software Engineer");

        var ex = await Assert.ThrowsAsync<PdfTextExtractionException>(
            () => PdfTextExtractor.ExtractTextAsync(pdf, TimeSpan.Zero));
        Assert.Contains("too long", ex.Message);
    }
}
