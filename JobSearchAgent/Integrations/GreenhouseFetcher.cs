using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JobSearchAgent.Integrations;

public class GreenhouseFetcher : IJobFetcher
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    // Slug -> display name. Add entries here as you find more AU companies on Greenhouse.
    // Verify a slug: https://boards-api.greenhouse.io/v1/boards/{slug}/jobs
    private static readonly Dictionary<string, string> Companies = new()
    {
        { "canva",         "Canva"          },
        { "xero",          "Xero"           },
        { "cultureamp",    "Culture Amp"    },
        { "safetyculture", "SafetyCulture"  },
        { "seek",          "Seek"           },
        { "airtasker",     "Airtasker"      },
        { "envato",        "Envato"         },
        { "finder",        "Finder"         },
        { "myob",          "MYOB"           },
        { "octopusdeploy", "Octopus Deploy" },
        { "realestate",    "REA Group"      },
    };

    public async Task<List<JobFeedItem>> FetchAllAsync()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<JobFeedItem>();

        foreach (var (slug, displayName) in Companies)
        {
            try
            {
                var items = await FetchCompanyAsync(slug, displayName);
                foreach (var item in items.Where(i => seen.Add(i.Url)))
                    results.Add(item);
                Console.WriteLine($"[Greenhouse] '{displayName}': {items.Count} AU results");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                Console.WriteLine($"[Greenhouse] '{slug}': slug not found, skipping");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Greenhouse] '{slug}': {ex.Message}");
            }
            await Task.Delay(300);
        }

        return results;
    }

    private async Task<List<JobFeedItem>> FetchCompanyAsync(string slug, string displayName)
    {
        var url = $"https://boards-api.greenhouse.io/v1/boards/{slug}/jobs?content=true";
        using var httpResponse = await _http.GetAsync(url);
        httpResponse.EnsureSuccessStatusCode();
        var bytes = await httpResponse.Content.ReadAsByteArrayAsync();
        var json = Encoding.UTF8.GetString(bytes);
        var response = JsonSerializer.Deserialize<GreenhouseResponse>(json)
            ?? throw new InvalidOperationException("Empty response from Greenhouse");

        return [.. response.Jobs
            .Where(j => IsAuLocation(j.Location?.Name))
            .Select(j => new JobFeedItem
            {
                Title       = j.Title,
                Company     = displayName,
                Url         = j.AbsoluteUrl,
                Description = JobFetcherUtils.StripHtml(j.Content ?? ""),
                Location    = j.Location?.Name ?? "",
                PublishedAt = j.UpdatedAt,
                Source      = "greenhouse",
            })];
    }

    private static bool IsAuLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return true;
        var lower = location.ToLowerInvariant();
        return JobFetcherUtils.AuLocationTokens.Any(lower.Contains);
    }

    private record GreenhouseResponse(
        [property: JsonPropertyName("jobs")] List<GreenhouseJob> Jobs
    );

    private record GreenhouseJob(
        [property: JsonPropertyName("title")]        string Title,
        [property: JsonPropertyName("absolute_url")] string AbsoluteUrl,
        [property: JsonPropertyName("updated_at")]   DateTime UpdatedAt,
        [property: JsonPropertyName("location")]     GreenhouseLocation? Location,
        [property: JsonPropertyName("content")]      string? Content
    );

    private record GreenhouseLocation(
        [property: JsonPropertyName("name")] string Name
    );
}
