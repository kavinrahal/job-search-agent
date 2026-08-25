using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace JobSearch.Api.Services;

public static class PdfRenderer
{
    static PdfRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    // Cover letters are plain paragraphs with no markdown syntax (see write_cover_letter.md's
    // "no markdown headers" rule), which Render already handles correctly as-is — each
    // non-blank line falls through to the plain-text branch below. Same renderer, named per
    // call site so it doesn't read as CV-specific where it's used for a letter.
    public static byte[] RenderLetter(string text) => Render(text);

    public static byte[] RenderCv(string markdown) => Render(markdown);

    private static byte[] Render(string markdown)
    {
        var lines = markdown.ReplaceLineEndings("\n").Split('\n');

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(2, Unit.Centimetre);
                page.MarginVertical(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9.5f).LineHeight(1.3f));
                page.Content().Column(col =>
                {
                    col.Spacing(2);
                    foreach (var raw in lines)
                        AddLine(col, raw);
                });
            });
        }).GeneratePdf();
    }

    private static void AddLine(ColumnDescriptor col, string raw)
    {
        // Check the bullet prefix against the untrimmed line, not a trailing-whitespace-stripped
        // one. ResumeRenderer.RenderBulletList (JobSearch.Data/ResumeRenderer.cs) always writes a
        // bullet as "- " + text, even when text is empty — so a cleared-to-empty achievement is
        // literally "- " (dash, one trailing space, nothing else). TrimEnd()-ing before this
        // check used to turn that into a bare "-", which failed the prefix match and fell
        // through to the plain-text branch below, printing a stray "-" line. Trailing/leading
        // whitespace is still stripped from the bullet's own text below, where it can't affect
        // the prefix check.
        if (raw.StartsWith("- ") || raw.StartsWith("* "))
        {
            var text = raw[2..].Trim();
            // An empty-text bullet has nothing to show — skip rendering it. Bullets here are
            // independent Row items with no shared list container (unlike the preview's <ul>),
            // so skipping one doesn't disturb the bullets before or after it.
            if (text.Length > 0)
            {
                col.Item().Row(row =>
                {
                    row.ConstantItem(12).Text("•");
                    row.RelativeItem().Text(t => Inline(t, text));
                });
            }
            return;
        }

        var line = raw.TrimEnd();

        if (line.StartsWith("# "))
        {
            col.Item().Text(line[2..]).FontSize(18).Bold();
        }
        else if (line.StartsWith("## "))
        {
            col.Item().PaddingTop(8).Column(inner =>
            {
                inner.Item().Text(line[3..]).FontSize(11).Bold();
                inner.Item().Height(1).Background("#AAAAAA");
            });
        }
        else if (line.StartsWith("### "))
        {
            col.Item().PaddingTop(4).Text(line[4..]).FontSize(10).Bold();
        }
        else if (line == "---")
        {
            col.Item().PaddingVertical(4).Height(0.5f).Background("#CCCCCC");
        }
        else if (string.IsNullOrWhiteSpace(line))
        {
            col.Item().Height(4);
        }
        else
        {
            col.Item().Text(t => Inline(t, line));
        }
    }

    private static void Inline(TextDescriptor text, string line)
    {
        var parts = Regex.Split(line, @"(\*\*[^*]+\*\*)");
        foreach (var part in parts)
        {
            if (part.StartsWith("**") && part.EndsWith("**") && part.Length > 4)
                text.Span(part[2..^2]).Bold();
            else if (part.Length > 0)
                text.Span(part);
        }
    }
}
