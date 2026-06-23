using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JobSearch.Data;

public class JobPostingFetcher
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly Regex SeekUrlPattern = new(
        @"au\.seek\.com/job/(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    static JobPostingFetcher()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Accept.ParseAdd(
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-AU,en;q=0.9");
    }

    public virtual async Task<string> FetchAsync(string url)
    {
        var seekMatch = SeekUrlPattern.Match(url);
        if (seekMatch.Success)
            return await FetchSeekAsync(seekMatch.Groups[1].Value);

        var html = await _http.GetStringAsync(url);
        return StripHtml(html);
    }

    private static async Task<string> FetchSeekAsync(string jobId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://chalice-experience.seek.com/api/job/{jobId}");
        request.Headers.Add("X-Seek-Site", "chalice-experience");

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var sb = new StringBuilder();

        if (root.TryGetProperty("title", out var title))
            sb.AppendLine($"Title: {title.GetString()}");

        if (root.TryGetProperty("advertiser", out var adv) &&
            adv.TryGetProperty("description", out var company))
            sb.AppendLine($"Company: {company.GetString()}");

        if (root.TryGetProperty("location", out var loc))
        {
            var locText = loc.ValueKind == JsonValueKind.String
                ? loc.GetString()
                : loc.TryGetProperty("label", out var ll) ? ll.GetString() : null;
            if (locText is not null) sb.AppendLine($"Location: {locText}");
        }

        if (root.TryGetProperty("salary", out var sal))
        {
            var salText = sal.ValueKind == JsonValueKind.String
                ? sal.GetString()
                : sal.TryGetProperty("label", out var sl) ? sl.GetString() : null;
            if (salText is not null) sb.AppendLine($"Salary: {salText}");
        }

        if (root.TryGetProperty("workArrangements", out var wa) &&
            wa.ValueKind == JsonValueKind.Object &&
            wa.TryGetProperty("data", out var waData) &&
            waData.ValueKind == JsonValueKind.Array)
        {
            var arrangements = waData.EnumerateArray()
                .Select(a => a.TryGetProperty("label", out var l) ? l.GetString() : null)
                .OfType<string>();
            var joined = string.Join(", ", arrangements);
            if (joined.Length > 0) sb.AppendLine($"Work arrangement: {joined}");
        }

        sb.AppendLine();

        if (root.TryGetProperty("bulletPoints", out var bullets) &&
            bullets.ValueKind == JsonValueKind.Array)
        {
            foreach (var b in bullets.EnumerateArray())
                sb.AppendLine($"• {b.GetString()}");
            sb.AppendLine();
        }

        if (root.TryGetProperty("content", out var content))
            sb.AppendLine(StripHtml(content.GetString() ?? ""));

        return sb.ToString().Trim();
    }

    private static string StripHtml(string html)
    {
        html = Regex.Replace(html,
            @"<(script|style|noscript)[^>]*>[\s\S]*?</(script|style|noscript)>",
            " ", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<!--[\s\S]*?-->", " ");
        html = Regex.Replace(html,
            @"<(br|p|div|li|h[1-6]|tr|td|th|section|article|header|footer)[^>]*>",
            "\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<[^>]+>", " ");
        html = System.Net.WebUtility.HtmlDecode(html);
        html = Regex.Replace(html, @"[ \t]{2,}", " ");
        html = Regex.Replace(html, @"\n{3,}", "\n\n");
        return html.Trim();
    }
}
