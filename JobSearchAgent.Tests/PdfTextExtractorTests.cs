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
}
