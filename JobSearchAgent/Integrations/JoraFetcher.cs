using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace JobSearchAgent.Integrations;

public class JoraFetcher : IJobFetcher
{
    private static readonly HttpClient _http;

    static JoraFetcher()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Add("Accept", "application/rss+xml, application/xml, text/xml, */*");
    }

    private static readonly string[] Keywords =
    [
        ".net",
        "react developer",
        "software engineer",
        "full stack developer",
        "full stack engineer",
    ];

    public async Task<List<JobFeedItem>> FetchAllAsync()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<JobFeedItem>();

        foreach (var keyword in Keywords)
        {
            try
            {
                var items = await FetchKeywordAsync(keyword);
                foreach (var item in items.Where(i => seen.Add(i.Url)))
                    results.Add(item);
                Console.WriteLine($"[Jora] '{keyword}': {items.Count} results");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Jora] Failed '{keyword}': {ex.Message}");
            }
            await Task.Delay(600);
        }

        return results;
    }

    private async Task<List<JobFeedItem>> FetchKeywordAsync(string keyword)
    {
        var url = $"https://au.jora.com/jobs?q={Uri.EscapeDataString(keyword)}" +
                  $"&l=Melbourne+VIC&rss=1";

        using var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync();
        var xml = System.Text.Encoding.UTF8.GetString(bytes);
        // Jora RSS contains bare & in URLs/text — escape any that aren't already part of a valid entity
        xml = Regex.Replace(xml, @"&(?!(?:[a-zA-Z][a-zA-Z0-9]*|#[0-9]+|#x[0-9a-fA-F]+);)", "&amp;");
        var doc = XDocument.Parse(xml);

        XNamespace? dc = doc.Root?.Attributes()
            .FirstOrDefault(a => a.Value == "http://purl.org/dc/elements/1.1/")
            ?.Name.LocalName is not null
            ? XNamespace.Get("http://purl.org/dc/elements/1.1/")
            : null;

        return [.. doc.Descendants("item").Select(item =>
        {
            var title    = item.Element("title")?.Value.Trim() ?? "";
            var link     = item.Element("link")?.Value.Trim() ?? "";
            var pubDate  = item.Element("pubDate")?.Value;
            var desc     = item.Element("description")?.Value ?? "";
            var company  = dc is not null
                ? item.Element(dc + "creator")?.Value.Trim() ?? ExtractCompany(title)
                : ExtractCompany(title);

            DateTime.TryParse(pubDate, out var published);

            return new JobFeedItem
            {
                Title       = ExtractTitle(title),
                Company     = company,
                Url         = link,
                Description = StripHtml(desc),
                Location    = "Melbourne VIC",
                PublishedAt = published == default ? DateTime.UtcNow : published,
                Source      = "jora",
            };
        }).Where(i => !string.IsNullOrEmpty(i.Url))];
    }

    // Jora RSS titles are often "Job Title at Company" or just "Job Title"
    private static string ExtractTitle(string title)
    {
        var idx = title.IndexOf(" at ", StringComparison.OrdinalIgnoreCase);
        return idx > 0 ? title[..idx].Trim() : title;
    }

    private static string ExtractCompany(string title)
    {
        var idx = title.IndexOf(" at ", StringComparison.OrdinalIgnoreCase);
        return idx > 0 ? title[(idx + 4)..].Trim() : "";
    }

    private static string StripHtml(string html)
    {
        var text = Regex.Replace(html, @"<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\s{2,}", " ").Trim();
    }
}
