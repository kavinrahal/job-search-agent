using System.Net;
using System.Text.RegularExpressions;

namespace JobSearchAgent.Integrations;

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

internal static class JobFetcherUtils
{
    internal static readonly string[] AuLocationTokens =
        ["melbourne", "vic", "victoria", "australia", "remote", "hybrid"];

    internal static string StripHtml(string html)
    {
        var text = Regex.Replace(html, @"<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\s{2,}", " ").Trim();
    }
}
