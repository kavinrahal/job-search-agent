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
