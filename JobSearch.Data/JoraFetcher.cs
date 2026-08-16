using System.Net;
using System.Text.RegularExpressions;

namespace JobSearch.Data;

// Jora has no public search API, unlike Adzuna — this scrapes their public search results
// page. Uses the "pretty URL" pattern (/{keywords}-jobs-in-{location}) rather than the
// query-string form (/j?q=...&l=...) — confirmed live from production that Jora blocks the
// query-string search endpoint (403) while leaving both individual job pages and this
// pretty-URL search form open. Used by the Seek/alert-email cross-check (JobAlertProcessor)
// and the Generate tab's manual "search Jora/Adzuna" fallback.
public class JoraFetcher
{
    private readonly HttpClient _http;
    public JoraFetcher() : this(new HttpClient { Timeout = TimeSpan.FromSeconds(15) }) { }
    public JoraFetcher(HttpClient http) { _http = http; }

    // ponytail: best-effort HTML scrape tied to Jora's current markup (the save-job button's
    // data-* attributes, which conveniently carry title/company/location together) — if Jora
    // restructures this, matches drop to zero rather than erroring, since SearchAsync just
    // returns fewer results. Upgrade to a real parser if that starts happening.
    private static readonly Regex CardPattern = new(
        "data-job-id=\"(?<jobId>[^\"]*)\"[^>]*data-tk=\"[^\"]*\"[^>]*data-saved=\"[^\"]*\"[^>]*" +
        "data-disabled=\"[^\"]*\"[^>]*data-ga4=\"[^\"]*\"[^>]*data-job-title=\"(?<title>[^\"]*)\"[^>]*" +
        "data-location=\"(?<location>[^\"]*)\"[^>]*data-company-name=\"(?<company>[^\"]*)\"",
        RegexOptions.Compiled);

    // A generic title search (e.g. "Software Engineer") has ~15 results per page and can run
    // 30+ pages deep — a single fetch only ever sees the first ~15, so a specific company's
    // listing is very unlikely to be in it (confirmed live: Jora accepts a "p" page param on
    // this same pretty-URL search — distinct from the query-string search form that's blocked
    // — so paging further is possible without hitting that block). Only paginate when a
    // company was given to look for — without one there's no criterion to decide "found it,
    // stop", and JobAlertProcessor's cross-check (context is a blended string, no separate
    // company) would otherwise pay for 5x the requests on every failed alert for no benefit.
    private const int MaxPagesWithCompany = 5;

    public virtual async Task<List<JobFeedItem>> SearchAsync(string keywords, string location, string? company = null)
    {
        var results = new List<JobFeedItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int maxPages = company is null ? 1 : MaxPagesWithCompany;

        for (int page = 1; page <= maxPages; page++)
        {
            var url = $"https://au.jora.com/{Slugify(keywords)}-jobs-in-{Slugify(location)}"
                + (page > 1 ? $"?p={page}" : "");
            string html;
            try
            {
                html = await _http.GetStringAsync(url);
            }
            catch
            {
                break;
            }

            var pageItems = ParseCards(html);
            if (pageItems.Count == 0) break;

            foreach (var item in pageItems.Where(i => seen.Add(i.Url)))
                results.Add(item);

            bool foundCompanyMatch = company is not null && pageItems.Any(i =>
                i.Company.Contains(company, StringComparison.OrdinalIgnoreCase) ||
                company.Contains(i.Company, StringComparison.OrdinalIgnoreCase));
            if (foundCompanyMatch) break;

            if (page < maxPages) await Task.Delay(400);
        }

        return results;
    }

    private static List<JobFeedItem> ParseCards(string html) =>
        [.. CardPattern.Matches(html)
            .Select(m => (Card: m, Href: FindHref(html, m.Groups["jobId"].Value)))
            .Where(x => x.Href is not null)
            .Select(x => new JobFeedItem
            {
                Title    = WebUtility.HtmlDecode(x.Card.Groups["title"].Value),
                Company  = WebUtility.HtmlDecode(x.Card.Groups["company"].Value),
                Location = WebUtility.HtmlDecode(x.Card.Groups["location"].Value),
                Url      = "https://au.jora.com" + WebUtility.HtmlDecode(x.Href),
                Source   = "jora",
            })];

    private static string? FindHref(string html, string jobId)
    {
        var pattern = "href=\"(/job/[^\"?]*" + Regex.Escape(jobId) + ")[^\"]*\"";
        var match = Regex.Match(html, pattern);
        return match.Success ? match.Groups[1].Value : null;
    }

    // "full stack .net developer" -> "full-stack-net-developer". Jora canonicalizes casing
    // and exact separators itself via a redirect, so this only needs to be close enough —
    // confirmed live that a plain lowercase/hyphenated guess like this lands correctly.
    private static string Slugify(string text)
    {
        var alphanumericAndSpaces = Regex.Replace(text, @"[^a-zA-Z0-9\s]", "");
        return Regex.Replace(alphanumericAndSpaces.Trim(), @"\s+", "-");
    }
}
