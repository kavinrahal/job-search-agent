using UglyToad.PdfPig;

namespace JobSearch.Data;

// Thrown only for a PDF this app cannot get usable text out of — corrupt bytes, or a scanned
// image with no text layer (PdfPig extracts text, it doesn't OCR). Deliberately its own type
// rather than a bare InvalidOperationException: ResumeIntakeAgent.ExtractField already throws
// that for a different, unrelated failure (a truncated Claude response), and the API layer
// needs to tell "bad input, tell the user" apart from "our bug, log it" without string-matching
// exception messages.
public class PdfTextExtractionException : Exception
{
    public PdfTextExtractionException() { }
    public PdfTextExtractionException(string message) : base(message) { }
    public PdfTextExtractionException(string message, Exception inner) : base(message, inner) { }
}

public static class PdfTextExtractor
{
    public static string ExtractText(byte[] pdfBytes)
    {
        string text;
        try
        {
            using var document = PdfDocument.Open(pdfBytes);
            text = string.Join("\n\n", document.GetPages().Select(p => p.Text));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new PdfTextExtractionException(
                "Couldn't read this PDF — it may be corrupted or password-protected.");
        }

        if (string.IsNullOrWhiteSpace(text))
            throw new PdfTextExtractionException(
                "Couldn't find any text in this PDF — it may be a scanned image with no text layer. Try pasting the resume text instead.");

        return text;
    }
}
