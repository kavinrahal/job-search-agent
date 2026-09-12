using UglyToad.PdfPig;

namespace JobSearch.Data;

// Thrown for any resume upload this app refuses to accept — corrupt/non-PDF bytes, a
// password-protected file, a scanned image with no text layer (PdfPig extracts text, it
// doesn't OCR), an oversized upload, or a file that fails the sniffing/sanity checks below
// (wrong magic bytes, implausible page count, extraction that ran too long). Deliberately its
// own type rather than a bare InvalidOperationException: ResumeIntakeAgent.ExtractField already
// throws that for a different, unrelated failure (a truncated Claude response), and the API
// layer needs to tell "bad input, tell the user" apart from "our bug, log it" without
// string-matching exception messages.
public class PdfTextExtractionException : Exception
{
    public PdfTextExtractionException() { }
    public PdfTextExtractionException(string message) : base(message) { }
    public PdfTextExtractionException(string message, Exception inner) : base(message, inner) { }
}

public static class PdfTextExtractor
{
    // Matches JobSearch.Api/Program.cs's global Kestrel MaxRequestBodySize — that limit exists
    // specifically to leave headroom for resume PDF uploads, so it's the one existing size
    // convention for this feature. Kept as a single source of truth here (Program.cs reads
    // this constant for Kestrel's limit) rather than two independently-maintained numbers.
    // A text-based resume is a few hundred KB at most; even a scanned/image-heavy one has no
    // business exceeding this.
    public const long MaxPdfBytes = 8_000_000;

    // A resume is a handful of pages. This is generous headroom over any real resume while
    // still bounding how much a hostile "valid PDF, absurd page count" file can make the
    // parser (and the CV-base/background pipeline downstream) churn through.
    private const int MaxPages = 50;

    // A well-formed, resume-sized PDF parses in well under a second. A file that hangs the
    // parser this long is treated as hostile rather than made to hang the request. Note: .NET
    // has no way to forcibly abort a running synchronous parse, so this bounds how long the
    // *request* waits (via Task.WaitAsync) — it does not guarantee the abandoned background
    // parse itself stops consuming a thread. Acceptable trade-off: it turns "request hangs
    // forever" into "request fails fast with a clear error," which is the actual DoS this
    // guards against, and the generation rate limiter on the upload endpoints bounds how many
    // such abandoned parses can pile up concurrently.
    private static readonly TimeSpan ExtractionTimeout = TimeSpan.FromSeconds(10);

    private static readonly byte[] PdfMagicBytes = "%PDF-"u8.ToArray();

    // Content-sniffing, not extension/Content-Type trust: both are attacker-controlled and
    // trivially spoofed. This checks the actual leading bytes of the file.
    public static bool HasPdfMagicBytes(byte[] bytes) =>
        bytes.Length >= PdfMagicBytes.Length && bytes.AsSpan(0, PdfMagicBytes.Length).SequenceEqual(PdfMagicBytes);

    public static Task<string> ExtractTextAsync(byte[] pdfBytes) =>
        ExtractTextAsync(pdfBytes, ExtractionTimeout);

    // Timeout as a parameter so tests can exercise the timeout path without a 10-second test.
    internal static async Task<string> ExtractTextAsync(byte[] pdfBytes, TimeSpan timeout)
    {
        if (pdfBytes.Length == 0)
            throw new PdfTextExtractionException("The uploaded file is empty.");
        if (pdfBytes.Length > MaxPdfBytes)
            throw new PdfTextExtractionException($"This PDF is too large — the max is {MaxPdfBytes / 1_000_000}MB.");
        if (!HasPdfMagicBytes(pdfBytes))
            throw new PdfTextExtractionException("This file doesn't look like a PDF.");

        string text;
        try
        {
            var parseTask = Task.Run(() => ExtractTextCore(pdfBytes));
            text = await parseTask.WaitAsync(timeout);
        }
        catch (TimeoutException)
        {
            throw new PdfTextExtractionException(
                "This PDF took too long to process — it may be malformed or unusually complex.");
        }
        catch (PdfTextExtractionException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new PdfTextExtractionException(
                "Couldn't read this PDF — it may be corrupted or password-protected.", ex);
        }

        if (string.IsNullOrWhiteSpace(text))
            throw new PdfTextExtractionException(
                "Couldn't find any text in this PDF — it may be a scanned image with no text layer. Try pasting the resume text instead.");

        return text;
    }

    // Kept as a sync wrapper for the (few) call sites/tests that don't need to await —
    // internally still goes through the async, timeout-bounded path.
    public static string ExtractText(byte[] pdfBytes) =>
        ExtractTextAsync(pdfBytes).GetAwaiter().GetResult();

    private static string ExtractTextCore(byte[] pdfBytes)
    {
        using var document = PdfDocument.Open(pdfBytes);
        var pages = document.GetPages().ToList();
        if (pages.Count > MaxPages)
            throw new PdfTextExtractionException(
                $"This PDF has {pages.Count} pages — that's more than a resume should need (max {MaxPages}).");
        return string.Join("\n\n", pages.Select(p => p.Text));
    }
}
