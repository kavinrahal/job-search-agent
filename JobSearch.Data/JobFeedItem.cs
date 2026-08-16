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
        return
        [
            .. candidates.OrderByDescending(c =>
                c.Company.Contains(company, StringComparison.OrdinalIgnoreCase) ||
                company.Contains(c.Company, StringComparison.OrdinalIgnoreCase)),
        ];
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
