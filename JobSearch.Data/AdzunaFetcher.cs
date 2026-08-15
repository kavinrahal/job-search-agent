using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JobSearch.Data;

public class AdzunaFetcher : IJobFetcher
{
    private readonly HttpClient _http;
    private readonly string _appId;
    private readonly string _appKey;

    public AdzunaFetcher(string appId, string appKey)
        : this(appId, appKey, new HttpClient { Timeout = TimeSpan.FromSeconds(15) }) { }

    public AdzunaFetcher(string appId, string appKey, HttpClient http)
    {
        _appId = appId;
        _appKey = appKey;
        _http = http;
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
                var items = await FetchKeywordAsync(keyword, "melbourne");
                foreach (var item in items.Where(i => seen.Add(i.Url)))
                    results.Add(item);
                Console.WriteLine($"[Adzuna] '{keyword}': {items.Count} results");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Adzuna] Failed '{keyword}': {ex.Message}");
            }
            await Task.Delay(400);
        }

        return results;
    }

    // Used for the Seek cross-check (JobAlertProcessor / the /cv,/letter,/answer generation
    // endpoints): a one-off targeted search rather than the fixed keyword sweep FetchAllAsync
    // runs for proactive discovery.
    public virtual async Task<List<JobFeedItem>> SearchAsync(string keywords, string location)
    {
        try
        {
            return await FetchKeywordAsync(keywords, location);
        }
        catch
        {
            return [];
        }
    }

    private async Task<List<JobFeedItem>> FetchKeywordAsync(string keyword, string location)
    {
#pragma warning disable S1075 // Adzuna public API base URL — not a configurable path
        var url = "https://api.adzuna.com/v1/api/jobs/au/search/1" +
            $"?app_id={Uri.EscapeDataString(_appId)}" +
            $"&app_key={Uri.EscapeDataString(_appKey)}" +
            $"&results_per_page=20" +
            $"&what={Uri.EscapeDataString(keyword)}" +
            $"&where={Uri.EscapeDataString(location)}" +
            $"&max_days_old=14" +
            $"&sort_by=date" +
            $"&content-type=application%2Fjson";
#pragma warning restore S1075

        using var httpResponse = await _http.GetAsync(url);
        httpResponse.EnsureSuccessStatusCode();
        var bytes = await httpResponse.Content.ReadAsByteArrayAsync();
        var json = Encoding.UTF8.GetString(bytes);
        var response = JsonSerializer.Deserialize<AdzunaResponse>(json)
            ?? throw new InvalidOperationException("Empty response from Adzuna");

        return [.. response.Results
            .Where(j => !string.IsNullOrEmpty(j.RedirectUrl))
            .Select(j => new JobFeedItem
            {
                Title       = j.Title,
                Company     = j.Company?.DisplayName ?? "",
                Url         = j.RedirectUrl,
                Description = j.Description,
                Location    = j.Location?.DisplayName ?? "",
                SalaryMin   = j.SalaryMin,
                SalaryMax   = j.SalaryMax,
                PublishedAt = j.Created,
                Source      = "adzuna",
            })];
    }

    private sealed record AdzunaResponse(
        [property: JsonPropertyName("results")] List<AdzunaJob> Results,
        [property: JsonPropertyName("count")]   int Count
    );

    private sealed record AdzunaJob(
        [property: JsonPropertyName("title")]        string Title,
        [property: JsonPropertyName("description")]  string Description,
        [property: JsonPropertyName("redirect_url")] string RedirectUrl,
        [property: JsonPropertyName("created")]      DateTime Created,
        [property: JsonPropertyName("location")]     AdzunaLocation? Location,
        [property: JsonPropertyName("company")]      AdzunaCompany? Company,
        [property: JsonPropertyName("salary_min")]   double? SalaryMin,
        [property: JsonPropertyName("salary_max")]   double? SalaryMax,
        [property: JsonPropertyName("contract_type")]string? ContractType,
        [property: JsonPropertyName("contract_time")]string? ContractTime
    );

    private sealed record AdzunaLocation([property: JsonPropertyName("display_name")] string DisplayName);
    private sealed record AdzunaCompany( [property: JsonPropertyName("display_name")] string DisplayName);
}
