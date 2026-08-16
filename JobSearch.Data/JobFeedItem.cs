using System.Net;
using System.Text.RegularExpressions;

namespace JobSearch.Data;

public class JobFeedItem
{
    public string Title { get; init; } = "";
    public string Company { get; init; } = "";
    public string Url { get; init; } = "";
    public string Description { get; init; } = "";
    public string Location { get; init; } = "";
    public double? SalaryMin { get; init; }
    public double? SalaryMax { get; init; }
    public DateTime PublishedAt { get; init; }
    public string Source { get; init; } = "";
}

public interface IJobFetcher
{
    Task<List<JobFeedItem>> FetchAllAsync();
}

public static class JobFetcherUtils
{
    public static readonly string[] AuLocationTokens =
        ["melbourne", "vic", "victoria", "australia", "remote", "hybrid"];

    // Null or empty location = globally remote/unspecified; include it.
    public static bool IsAuLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return true;
        var lower = location.ToLowerInvariant();
        return AuLocationTokens.Any(lower.Contains);
    }

    public static string StripHtml(string html)
    {
        var text = Regex.Replace(html, @"<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\s{2,}", " ").Trim();
    }

    // Either name containing the other — handles abbreviations ("Codafication" vs "Codafication
    // Pty Ltd") in both directions.
    private static bool MatchesCompany(JobFeedItem item, string company) =>
        item.Company.Contains(company, StringComparison.OrdinalIgnoreCase) ||
        company.Contains(item.Company, StringComparison.OrdinalIgnoreCase);

    // Reorders (never filters) search results so a company-name match comes first — used when
    // a user supplies both a job title and a company for the Generate-tab candidate search.
    // Company match is intentionally not part of the search keywords themselves: Jora/Adzuna's
    // keyword search ranks worse when a company name is blended into the title query (confirmed
    // — "software engineer codafication" missed a real Codafication listing that a plain
    // "software engineer" search surfaces easily), so title stays the sole search term and
    // company only re-ranks what came back.
    public static List<JobFeedItem> RankByCompany(List<JobFeedItem> candidates, string? company)
    {
        if (string.IsNullOrWhiteSpace(company)) return candidates;
        return [.. candidates.OrderByDescending(c => MatchesCompany(c, company))];
    }

    // Shared pagination shape for JoraFetcher/AdzunaFetcher's company-driven search: only page
    // beyond 1 when a company was given — without one there's no criterion to decide "found it,
    // stop", and JobAlertProcessor's cross-check (a blended context string, no separate company)
    // would otherwise pay for the full page cap in requests on every failed alert for no
    // benefit. Stops as soon as a page contains a company match, so the common case doesn't
    // always pay for the worst case either.
    public static async Task<List<JobFeedItem>> PaginateWithCompanyMatch(
        string? company, int maxPagesWithCompany, Func<int, Task<List<JobFeedItem>>> fetchPage)
    {
        var results = new List<JobFeedItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int maxPages = company is null ? 1 : maxPagesWithCompany;

        for (int page = 1; page <= maxPages; page++)
        {
            List<JobFeedItem> pageItems;
            try
            {
                pageItems = await fetchPage(page);
            }
            catch
            {
                break;
            }
            if (pageItems.Count == 0) break;

            foreach (var item in pageItems.Where(i => seen.Add(i.Url)))
                results.Add(item);

            if (company is not null && pageItems.Any(i => MatchesCompany(i, company)))
                break;

            if (page < maxPages) await Task.Delay(300);
        }

        return results;
    }

    // Shared by JobDiscoveryWorker (feed items too short to warrant a full page fetch) and
    // JobAlertProcessor's Seek cross-check (a matched Jora/Adzuna candidate) — same shape
    // either way: a JobFeedItem standing in for a posting's full text.
    public static string ToPostingText(this JobFeedItem item)
    {
        string salary;
        if (item.SalaryMin.HasValue && item.SalaryMax.HasValue)
            salary = $"${item.SalaryMin:N0} – ${item.SalaryMax:N0} AUD";
        else if (item.SalaryMin.HasValue)
            salary = $"From ${item.SalaryMin:N0} AUD";
        else
            salary = "Not stated";

        return $"""
            Company: {item.Company}
            Job Title: {item.Title}
            Location: {item.Location}
            Salary: {salary}

            {item.Description}
            """;
    }
}
