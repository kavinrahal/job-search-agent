using System.Xml.Linq;

namespace JobSearchAgent.Integrations;

public class SeekRssFeedItem
{
    public string Title { get; init; } = "";
    public string Url { get; init; } = "";
    public DateTime PublishedAt { get; init; }
}

public class SeekRssFetcher
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    static SeekRssFetcher()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/rss+xml, application/xml, text/xml, */*");
        _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-AU,en;q=0.9");
    }

    // Keywords × location combinations to poll.
    // Remote searches omit the Melbourne location to catch AU-wide remote roles.
    private static readonly (string Keywords, bool RemoteOnly)[] Searches =
    [
        (".net",                  false),
        ("react developer",       false),
        ("software engineer",     false),
        ("full stack developer",  false),
        ("full stack engineer",   false),
        (".net",                  true),
        ("software engineer",     true),
        ("full stack developer",  false),
    ];

    public async Task<List<SeekRssFeedItem>> FetchAllAsync()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<SeekRssFeedItem>();

        foreach (var (keywords, remoteOnly) in Searches)
        {
            var feedUrl = BuildUrl(keywords, remoteOnly);
            try
            {
                var items = await FetchFeedAsync(feedUrl);
                foreach (var item in items.Where(i => seen.Add(i.Url)))
                    results.Add(item);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Seek RSS] Failed '{keywords}' (remote={remoteOnly}): {ex.Message}");
            }

            await Task.Delay(600); // polite gap between requests
        }

        return results;
    }

    private static string BuildUrl(string keywords, bool remoteOnly)
    {
        var encoded = Uri.EscapeDataString(keywords);
        return remoteOnly
            ? $"https://www.seek.com.au/jobs?keywords={encoded}&worktype=work_from_home&rss=true"
            : $"https://www.seek.com.au/jobs?keywords={encoded}&where=All+Melbourne+VIC&rss=true";
    }

    private static async Task<List<SeekRssFeedItem>> FetchFeedAsync(string feedUrl)
    {
        var xml = await _http.GetStringAsync(feedUrl);
        var doc = XDocument.Parse(xml);

        return [.. doc
            .Descendants("item")
            .Select(item =>
            {
                var title = item.Element("title")?.Value.Trim() ?? "";

                // <link> in RSS 2.0 is a text node sibling, not an element value in XLinq —
                // fall back to <guid> which is always a proper element.
                var link = item.Elements()
                    .Where(e => e.Name.LocalName == "link")
                    .Select(e => e.Value.Trim())
                    .FirstOrDefault(v => v.StartsWith("http"))
                    ?? item.Elements()
                        .Where(e => e.Name.LocalName == "guid")
                        .Select(e => e.Value.Trim())
                        .FirstOrDefault(v => v.StartsWith("http"))
                    ?? "";

                DateTime published = DateTime.UtcNow;
                var pubDate = item.Element("pubDate")?.Value;
                if (pubDate is not null && DateTime.TryParse(pubDate, out var pd))
                    published = pd.ToUniversalTime();

                return new SeekRssFeedItem { Title = title, Url = link, PublishedAt = published };
            })
            .Where(i => !string.IsNullOrEmpty(i.Url))];
    }
}
