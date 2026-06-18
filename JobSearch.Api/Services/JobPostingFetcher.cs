using System.Text.RegularExpressions;

namespace JobSearch.Api.Services;

public class JobPostingFetcher
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(20),
    };

    static JobPostingFetcher()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Accept.ParseAdd(
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-AU,en;q=0.9");
    }

    public async Task<string> FetchAsync(string url)
    {
        var html = await _http.GetStringAsync(url);
        return StripHtml(html);
    }

    private static string StripHtml(string html)
    {
        // Remove <script> and <style> blocks entirely
        html = Regex.Replace(html,
            @"<(script|style|noscript)[^>]*>[\s\S]*?</(script|style|noscript)>",
            " ", RegexOptions.IgnoreCase);

        // Remove HTML comments
        html = Regex.Replace(html, @"<!--[\s\S]*?-->", " ");

        // Replace block-level tags with newlines
        html = Regex.Replace(html,
            @"<(br|p|div|li|h[1-6]|tr|td|th|section|article|header|footer)[^>]*>",
            "\n", RegexOptions.IgnoreCase);

        // Strip remaining tags
        html = Regex.Replace(html, @"<[^>]+>", " ");

        // Decode HTML entities
        html = System.Net.WebUtility.HtmlDecode(html);

        // Collapse excessive whitespace while preserving paragraph breaks
        html = Regex.Replace(html, @"[ \t]{2,}", " ");
        html = Regex.Replace(html, @"\n{3,}", "\n\n");

        return html.Trim();
    }
}
