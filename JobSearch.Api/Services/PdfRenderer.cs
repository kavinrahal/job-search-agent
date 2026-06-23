using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace JobSearch.Api.Services;

public static class PdfRenderer
{
    static PdfRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] RenderCv(string markdown)
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
                        AddLine(col, raw.TrimEnd());
                });
            });
        }).GeneratePdf();
    }

    private static void AddLine(ColumnDescriptor col, string line)
    {
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
        else if (line.StartsWith("- ") || line.StartsWith("* "))
        {
            col.Item().Row(row =>
            {
                row.ConstantItem(12).Text("•");
                row.RelativeItem().Text(t => Inline(t, line[2..]));
            });
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
