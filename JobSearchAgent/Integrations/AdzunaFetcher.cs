using System.Text.Json;
using System.Text.Json.Serialization;

namespace JobSearchAgent.Integrations;

public class AdzunaFeedItem
{
    public string Title { get; init; } = "";
    public string Company { get; init; } = "";
    public string Url { get; init; } = "";
    public string Description { get; init; } = "";
    public string Location { get; init; } = "";
    public double? SalaryMin { get; init; }
    public double? SalaryMax { get; init; }
    public DateTime PublishedAt { get; init; }
}

public class AdzunaFetcher
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly string _appId;
    private readonly string _appKey;

    private static readonly string[] Keywords =
    [
        ".net",
        "react developer",
        "software engineer",
        "full stack developer",
        "full stack engineer",
    ];

    public AdzunaFetcher(string appId, string appKey)
    {
        _appId = appId;
        _appKey = appKey;
    }

    public async Task<List<AdzunaFeedItem>> FetchAllAsync()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<AdzunaFeedItem>();

        foreach (var keyword in Keywords)
        {
            try
            {
                var items = await FetchKeywordAsync(keyword);
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

    private async Task<List<AdzunaFeedItem>> FetchKeywordAsync(string keyword)
    {
        var url = "https://api.adzuna.com/v1/api/jobs/au/search/1" +
            $"?app_id={Uri.EscapeDataString(_appId)}" +
            $"&app_key={Uri.EscapeDataString(_appKey)}" +
            $"&results_per_page=20" +
            $"&what={Uri.EscapeDataString(keyword)}" +
            $"&where=melbourne" +
            $"&max_days_old=14" +
            $"&sort_by=date" +
            $"&content-type=application%2Fjson";

        var json = await _http.GetStringAsync(url);
        var response = JsonSerializer.Deserialize<AdzunaResponse>(json)
            ?? throw new InvalidOperationException("Empty response from Adzuna");

        return [.. response.Results
            .Where(j => !string.IsNullOrEmpty(j.RedirectUrl))
            .Select(j => new AdzunaFeedItem
            {
                Title       = j.Title,
                Company     = j.Company?.DisplayName ?? "",
                Url         = j.RedirectUrl,
                Description = j.Description,
                Location    = j.Location?.DisplayName ?? "",
                SalaryMin   = j.SalaryMin,
                SalaryMax   = j.SalaryMax,
                PublishedAt = j.Created,
            })];
    }

    // ---------------------------------------------------------------------------
    // Internal deserialization types
    // ---------------------------------------------------------------------------
    private record AdzunaResponse(
        [property: JsonPropertyName("results")] List<AdzunaJob> Results,
        [property: JsonPropertyName("count")]   int Count
    );

    private record AdzunaJob(
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

    private record AdzunaLocation([property: JsonPropertyName("display_name")] string DisplayName);
    private record AdzunaCompany( [property: JsonPropertyName("display_name")] string DisplayName);
}
