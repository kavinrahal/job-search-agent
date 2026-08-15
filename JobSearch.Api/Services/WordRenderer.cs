using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace JobSearch.Api.Services;

public static class WordRenderer
{
    // Cover letters are plain paragraphs, one per non-blank line (see write_cover_letter.md's
    // "no markdown headers" rule) — same line-is-a-paragraph shape PdfRenderer.RenderLetter
    // relies on, just built as real Word paragraphs instead of PDF text blocks.
    public static byte[] RenderLetter(string text)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

#pragma warning disable S3220 // Open XML SDK's params/IEnumerable overload pair on every element constructor — not ambiguous, just how the SDK is shaped.
            foreach (var line in text.ReplaceLineEndings("\n").Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var paragraph = new Paragraph
                {
                    ParagraphProperties = new ParagraphProperties(new SpacingBetweenLines { After = "240" }),
                };
                paragraph.Append(new Run(new Text(line.Trim()) { Space = SpaceProcessingModeValues.Preserve }));
                body.Append(paragraph);
            }
#pragma warning restore S3220

            mainPart.Document.Save();
        }
        return stream.ToArray();
    }
}
