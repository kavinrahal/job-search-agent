using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobSearch.Data;

namespace JobSearchAgent.Integrations;

public class LeverFetcher : IJobFetcher
{
    private readonly HttpClient _http;
    public LeverFetcher() : this(new HttpClient { Timeout = TimeSpan.FromSeconds(15) }) { }
    internal LeverFetcher(HttpClient http) { _http = http; }

    // Slug -> display name. Add entries here as you find more AU companies on Lever.
    // Verify a slug before adding it: GET https://api.lever.co/v0/postings/{slug}?mode=json must
    // return 200 (a 404 means the slug is wrong or the company isn't on Lever) - a plausible-
    // looking slug is not enough, actually curl it and check the response.
    //
    // Last fully re-verified against the live API: 2026-09-12. At that point the then-current
    // list (atlassian, afterpay, buildkite, bugcrowd, redbubble, squarespace) 404'd entirely on
    // Lever - atlassian is on SmartRecruiters, afterpay's current employer entity Block posts AU
    // roles on Greenhouse instead (see GreenhouseFetcher's "block" entry), buildkite/bugcrowd/
    // squarespace turned out to be real slugs but on Greenhouse not Lever (moved there), and
    // redbubble's current ATS could not be confirmed as Greenhouse or Lever (removed rather than
    // guessed).
    private static readonly Dictionary<string, string> Companies = new()
    {
        { "safetyculture-2", "SafetyCulture" },
        { "airtasker",       "Airtasker"     },
        { "envato-2",        "Envato"        },
        { "deputy",          "Deputy"        },
        { "immutable",       "Immutable"     },
        { "upguard",         "UpGuard"       },
        { "mable",           "Mable"         },
        { "brighte",         "Brighte"       },
        { "plenti",          "Plenti"        },
        { "splend",          "Splend"        },
        { "kogan",           "Kogan"         },
        { "wisr",            "Wisr"          },
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
                Console.WriteLine($"[Lever] '{displayName}': {items.Count} AU results");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                Console.WriteLine($"[Lever] '{slug}': slug not found, skipping");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Lever] '{slug}': {ex.Message}");
            }
            await Task.Delay(300);
        }

        return results;
    }

    private async Task<List<JobFeedItem>> FetchCompanyAsync(string slug, string displayName)
    {
        var url = $"https://api.lever.co/v0/postings/{slug}?mode=json";
        using var httpResponse = await _http.GetAsync(url);
        httpResponse.EnsureSuccessStatusCode();
        var bytes = await httpResponse.Content.ReadAsByteArrayAsync();
        var json = Encoding.UTF8.GetString(bytes);
        var postings = JsonSerializer.Deserialize<List<LeverPosting>>(json)
            ?? throw new InvalidOperationException("Empty response from Lever");

        return [.. postings
            .Where(p => IsAuLocation(p.Categories?.Location))
            .Select(p => new JobFeedItem
            {
                Title       = p.Text,
                Company     = displayName,
                Url         = p.HostedUrl,
                Description = BuildDescription(p),
                Location    = p.Categories?.Location ?? "",
                PublishedAt = DateTimeOffset.FromUnixTimeMilliseconds(p.CreatedAt).UtcDateTime,
                Source      = "lever",
            })];
    }

    // Lever splits a posting's real content across several fields, not just `description`:
    // `lists` holds titled sections (confirmed live — "What We Require", "What We Value", etc.)
    // that carry the actual skills/qualifications requirements, and `additional` carries benefits/
    // EEO boilerplate, which is exactly where visa-sponsorship language tends to live ("we are
    // unable to sponsor..."). Only concatenating `description` (the intro blurb) silently dropped
    // both — the evaluator never saw them at all, regardless of prompt or schema.
    private static string BuildDescription(LeverPosting p)
    {
        var sections = new List<string>();

        var main = !string.IsNullOrEmpty(p.DescriptionPlain)
            ? p.DescriptionPlain
            : JobFetcherUtils.StripHtml(p.Description ?? "");
        if (!string.IsNullOrWhiteSpace(main)) sections.Add(main);

        foreach (var list in p.Lists ?? [])
        {
            var body = JobFetcherUtils.StripHtml(list.Content ?? "");
            if (string.IsNullOrWhiteSpace(body)) continue;
            sections.Add(string.IsNullOrWhiteSpace(list.Text) ? body : $"{list.Text}:\n{body}");
        }

        var additional = !string.IsNullOrEmpty(p.AdditionalPlain)
            ? p.AdditionalPlain
            : JobFetcherUtils.StripHtml(p.Additional ?? "");
        if (!string.IsNullOrWhiteSpace(additional)) sections.Add(additional);

        return string.Join("\n\n", sections);
    }

    private static bool IsAuLocation(string? location) => JobFetcherUtils.IsAuLocation(location);

    private sealed record LeverPosting(
        [property: JsonPropertyName("id")]               string Id,
        [property: JsonPropertyName("text")]             string Text,
        [property: JsonPropertyName("hostedUrl")]        string HostedUrl,
        [property: JsonPropertyName("description")]      string? Description,
        [property: JsonPropertyName("descriptionPlain")] string? DescriptionPlain,
        [property: JsonPropertyName("lists")]             List<LeverList>? Lists,
        [property: JsonPropertyName("additional")]        string? Additional,
        [property: JsonPropertyName("additionalPlain")]  string? AdditionalPlain,
        [property: JsonPropertyName("createdAt")]        long CreatedAt,
        [property: JsonPropertyName("categories")]       LeverCategories? Categories
    );

    private sealed record LeverList(
        [property: JsonPropertyName("text")]    string? Text,
        [property: JsonPropertyName("content")] string? Content
    );

    private sealed record LeverCategories(
        [property: JsonPropertyName("location")] string? Location,
        [property: JsonPropertyName("team")]     string? Team
    );
}
