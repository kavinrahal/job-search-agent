using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace JobSearchAgent.Integrations;

public class LeverFetcher : IJobFetcher
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    // Slug -> display name. Add entries here as you find more AU companies on Lever.
    // Verify a slug: https://api.lever.co/v0/postings/{slug}?mode=json
    private static readonly Dictionary<string, string> Companies = new()
    {
        { "atlassian",    "Atlassian"    },
        { "afterpay",     "Afterpay"     },
        { "buildkite",    "Buildkite"    },
        { "bugcrowd",     "Bugcrowd"     },
        { "redbubble",    "Redbubble"    },
        { "squarespace",  "Squarespace"  },
    };

    private static readonly string[] AuLocationTokens =
        ["melbourne", "vic", "victoria", "australia", "remote", "hybrid"];

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
                Description = !string.IsNullOrEmpty(p.DescriptionPlain)
                    ? p.DescriptionPlain
                    : StripHtml(p.Description ?? ""),
                Location    = p.Categories?.Location ?? "",
                PublishedAt = DateTimeOffset.FromUnixTimeMilliseconds(p.CreatedAt).UtcDateTime,
                Source      = "lever",
            })];
    }

    private static bool IsAuLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return true;
        var lower = location.ToLowerInvariant();
        return AuLocationTokens.Any(lower.Contains);
    }

    private static string StripHtml(string html)
    {
        var text = Regex.Replace(html, @"<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\s{2,}", " ").Trim();
    }

    private record LeverPosting(
        [property: JsonPropertyName("id")]               string Id,
        [property: JsonPropertyName("text")]             string Text,
        [property: JsonPropertyName("hostedUrl")]        string HostedUrl,
        [property: JsonPropertyName("description")]      string? Description,
        [property: JsonPropertyName("descriptionPlain")] string? DescriptionPlain,
        [property: JsonPropertyName("createdAt")]        long CreatedAt,
        [property: JsonPropertyName("categories")]       LeverCategories? Categories
    );

    private record LeverCategories(
        [property: JsonPropertyName("location")] string? Location,
        [property: JsonPropertyName("team")]     string? Team
    );
}
