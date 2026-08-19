using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace JobSearch.Data;

public class JobPostingFetcher
{
    // ConnectCallback is the real enforcement layer, not just the upfront URL check below —
    // HttpClient follows redirects by default, and a malicious server could otherwise redirect
    // a validated public URL to an internal address. This runs on every connection attempt,
    // including each redirect hop, and re-resolves DNS fresh at connect time rather than
    // trusting an earlier lookup (closing the DNS-rebinding TOCTOU gap a text/URL-only check
    // would leave open).
    private static readonly HttpClient _http = new(new SocketsHttpHandler { ConnectCallback = ConnectPublicOnlyAsync })
    {
        Timeout = TimeSpan.FromSeconds(20),
    };

    private static async ValueTask<Stream> ConnectPublicOnlyAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        var address = addresses.FirstOrDefault(IsPubliclyRoutable)
            ?? throw new InvalidOperationException($"'{context.DnsEndPoint.Host}' does not resolve to a public address.");

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(address, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    // Blocks loopback, link-local (including the 169.254.169.254 cloud metadata address),
    // and the RFC1918 private ranges. Not a public-vs-private allowlist beyond that — this app
    // legitimately fetches arbitrary public job-board URLs, only internal/infra addresses need
    // blocking.
    internal static bool IsPubliclyRoutable(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return false;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            if (b[0] == 0) return false;                              // 0.0.0.0/8
            if (b[0] == 10) return false;                              // 10.0.0.0/8
            if (b[0] == 172 && b[1] is >= 16 and <= 31) return false;  // 172.16.0.0/12
            if (b[0] == 192 && b[1] == 168) return false;              // 192.168.0.0/16
            if (b[0] == 169 && b[1] == 254) return false;              // 169.254.0.0/16 (incl. cloud metadata)
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            return !ip.IsIPv6LinkLocal && !ip.IsIPv6SiteLocal && !ip.IsIPv6UniqueLocal;

        return false;
    }

    private static async Task<bool> IsPubliclyRoutableAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(uri.Host);
            return addresses.Length > 0 && addresses.All(IsPubliclyRoutable);
        }
        catch
        {
            return false;
        }
    }

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
    // Links copied from a search results page carry tracking query params (search position,
    // session/referral tokens) that identify the browsing session that generated them —
    // Cloudflare on these sites can treat a token replayed from an unrelated IP (this app's
    // server, no matching session) as suspicious and block a request that would succeed
    // against the bare canonical URL. These hosts' job pages render identically without a
    // query string (see JobAlertProcessor's canonical seek/linkedin/jora URLs), so stripping
    // it first is strictly safer — left alone for any other host, where a query string might
    // be load-bearing (e.g. a Greenhouse/Lever board's own paging or embed params).
    private static readonly HashSet<string> TrackingParamHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "au.jora.com", "au.seek.com", "www.linkedin.com",
    };

    public static string StripTrackingParams(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return url;
        if (!TrackingParamHosts.Contains(uri.Host)) return url;
        if (uri.Query.Length == 0 && uri.Fragment.Length == 0) return url;
        return uri.GetLeftPart(UriPartial.Path);
    }

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
        url = StripTrackingParams(url);

        // postingUrl comes straight from an authenticated user's request body — without this,
        // it's a same-origin-network SSRF: a user could point it at cloud metadata endpoints
        // (169.254.169.254) or internal Railway service addresses and have this server fetch
        // it on their behalf. Resolves the host and blocks anything private/loopback/link-local
        // before ever making the request, rather than only checking the URL's literal text
        // (which a redirect or DNS trickery could route around a text-only check anyway).
        if (!await IsPubliclyRoutableAsync(url))
        {
            var blocked = new AttemptResult(null, null, false, "URL not allowed");
            return new FetchDiagnostics(blocked, null, null);
        }

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
