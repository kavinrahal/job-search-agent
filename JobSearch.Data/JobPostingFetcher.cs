using System.Text.RegularExpressions;

namespace JobSearch.Data;

public class JobPostingFetcher
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    static JobPostingFetcher()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Accept.ParseAdd(
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-AU,en;q=0.9");
        // A Chrome UA with none of its usual companion headers is itself a bot signal to
        // WAFs like Cloudflare Bot Management (which Seek runs) — these round it out.
        _http.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
        _http.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
        _http.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
        _http.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
        _http.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
    }

    // Cloudflare/Akamai-style bot management (confirmed on Seek: __cf_bm cookie, CF-Ray
    // header) blocks or challenges requests from datacenter IPs like this app's host
    // regardless of headers, even though the same request succeeds from a residential IP.
    // r.jina.ai fetches the page on our behalf from IPs that generally aren't on those
    // blocklists and returns cleaned text directly — a well-known workaround for exactly
    // this class of failure, used only as a fallback so most sites never touch it.
    public virtual async Task<string> FetchAsync(string url)
    {
        var diagnostics = await DiagnoseAsync(url);
        return diagnostics.ResultText
            ?? throw new HttpRequestException(
                $"fetch failed (direct: {diagnostics.Direct.Error ?? $"status {diagnostics.Direct.StatusCode}, looked like a challenge page"}; " +
                $"reader: {diagnostics.Reader?.Error ?? $"status {diagnostics.Reader?.StatusCode}"})");
    }

    public sealed record AttemptResult(int? StatusCode, int? ContentLength, bool LooksLikeChallenge, string? Error);
    public sealed record FetchDiagnostics(AttemptResult Direct, AttemptResult? Reader, string? ResultText);

    // Reports what actually happened on each attempt (status code, size, whether the response
    // looks like a bot-challenge page), not just the final text — so a source's reachability
    // can be confirmed from the deployed environment itself, rather than inferred from
    // behavior observed elsewhere (e.g. a developer's own machine, which has a different IP
    // reputation than where this runs in production). FetchAsync is a thin wrapper over this.
    public virtual async Task<FetchDiagnostics> DiagnoseAsync(string url)
    {
        int? directStatus = null, directLength = null;
        bool directChallenge = false;
        string? directError = null, resultText = null;

        try
        {
            using var response = await _http.GetAsync(url);
            directStatus = (int)response.StatusCode;
            var html = await response.Content.ReadAsStringAsync();
            directLength = html.Length;
            response.EnsureSuccessStatusCode();
            directChallenge = LooksLikeBotChallenge(html);
            if (!directChallenge) resultText = StripHtml(html);
        }
        catch (Exception ex)
        {
            directError = ex.Message;
        }

        var direct = new AttemptResult(directStatus, directLength, directChallenge, directError);
        if (resultText is not null)
            return new FetchDiagnostics(direct, null, resultText);

        int? readerStatus = null, readerLength = null;
        bool readerChallenge = false;
        string? readerError = null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://r.jina.ai/{url}");
            using var response = await _http.SendAsync(request);
            readerStatus = (int)response.StatusCode;
            var text = await response.Content.ReadAsStringAsync();
            readerLength = text.Length;
            response.EnsureSuccessStatusCode();
            readerChallenge = LooksLikeBotChallenge(text);
            if (!readerChallenge) resultText = text.Trim();
        }
        catch (Exception ex)
        {
            readerError = ex.Message;
        }

        var reader = new AttemptResult(readerStatus, readerLength, readerChallenge, readerError);
        return new FetchDiagnostics(direct, reader, resultText);
    }

    // Bot-challenge interstitials (Cloudflare "Just a moment...", etc.) are short and carry
    // unmistakable markers; a real job posting page is thousands of characters and never
    // contains this exact copy.
    private static bool LooksLikeBotChallenge(string html)
    {
        if (html.Length < 3000) return true;
        return html.Contains("Just a moment", StringComparison.OrdinalIgnoreCase)
            || html.Contains("cf-browser-verification", StringComparison.OrdinalIgnoreCase)
            || html.Contains("Checking your browser", StringComparison.OrdinalIgnoreCase)
            || html.Contains("Enable JavaScript and cookies to continue", StringComparison.OrdinalIgnoreCase);
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
