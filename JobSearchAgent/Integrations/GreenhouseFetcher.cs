using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobSearch.Data;

namespace JobSearchAgent.Integrations;

public class GreenhouseFetcher : IJobFetcher
{
    private readonly HttpClient _http;
    public GreenhouseFetcher() : this(new HttpClient { Timeout = TimeSpan.FromSeconds(15) }) { }
    internal GreenhouseFetcher(HttpClient http) { _http = http; }

    // Slug -> display name. Add entries here as you find more AU companies on Greenhouse.
    // Verify a slug before adding it: GET https://boards-api.greenhouse.io/v1/boards/{slug}/jobs
    // must return 200 (a 404 body of {"status":404,"error":"Job not found"} means the slug is
    // wrong, or the company isn't on Greenhouse at all) - a plausible-looking slug is not enough,
    // actually curl it and check the response.
    //
    // Last fully re-verified against the live API: 2026-09-12. At that point most of the
    // then-current list (canva, xero, safetyculture, seek, airtasker, envato, finder, myob,
    // realestate) 404'd - those companies had either migrated ATS (canva/atlassian -> SmartRecruiters,
    // realestate/REA Group -> Workday) or never used the guessed slug (xero, finder, myob run
    // custom/other career sites). safetyculture/airtasker/envato turned out to be real, just on
    // Lever instead of Greenhouse - see LeverFetcher. Dead entries were removed rather than kept,
    // since a bigger broken list isn't better than a smaller working one.
    private static readonly Dictionary<string, string> Companies = new()
    {
        { "cultureamp",     "Culture Amp"     },
        { "octopusdeploy",  "Octopus Deploy"  },
        { "buildkite",      "Buildkite"       },
        { "bugcrowd",       "Bugcrowd"        },
        { "squarespace",    "Squarespace"     },
        { "block",          "Block"           }, // parent co. of Afterpay/Square/Cash App AU
        { "prospa",         "Prospa"          },
        { "eucalyptus",     "Eucalyptus"      },
        { "sendle",         "Sendle"          },
        { "spaceship",      "Spaceship"       },
        { "karbon",         "Karbon"          },
        { "mx51",           "mx51"            },
        { "airlockdigital", "Airlock Digital" },
        { "go1au",          "Go1"             },
        { "zipcolimited",   "Zip Co"          },
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

    private static bool IsAuLocation(string? location) => JobFetcherUtils.IsAuLocation(location);

    private sealed record GreenhouseResponse(
        [property: JsonPropertyName("jobs")] List<GreenhouseJob> Jobs
    );

    private sealed record GreenhouseJob(
        [property: JsonPropertyName("title")]        string Title,
        [property: JsonPropertyName("absolute_url")] string AbsoluteUrl,
        [property: JsonPropertyName("updated_at")]   DateTime UpdatedAt,
        [property: JsonPropertyName("location")]     GreenhouseLocation? Location,
        [property: JsonPropertyName("content")]      string? Content
    );

    private sealed record GreenhouseLocation(
        [property: JsonPropertyName("name")] string Name
    );
}
