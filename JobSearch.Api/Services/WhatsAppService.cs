using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JobSearch.Api.Services;

public record WhatsAppUpdate(string MessageId, string? From, string? Text, string? ContextId, bool IsStatusUpdate);

public class WhatsAppService
{
    private readonly HttpClient _http = new();
    private readonly string? _apiBase;
    private readonly string? _toNumber;
    private readonly string? _appSecret;
    private readonly string? _verifyToken;
    private readonly string _templateName;
    private readonly string _templateLang;

    // Tracks processed WhatsApp message ids (wamid) to prevent duplicate processing on retries.
    private readonly ConcurrentDictionary<string, byte> _processed = new();

    // Tracks wamids of "couldn't fetch, paste the description" prompts we've sent, so a
    // reply to one can be resolved back to the command/URL it was about — WhatsApp's
    // webhook only gives a context.id, never the replied-to text (unlike Telegram).
    // In-memory only, like _processed: fine to lose on restart, the user just gets a
    // normal "please include a job URL" response instead of the paste-fallback continuing.
    private readonly ConcurrentDictionary<string, (string Command, string Url)> _pendingPasteFallback = new();

    // True only if every value needed for both sending and receiving is present.
    // Every public method no-ops (returns null/false) when this is false, so a
    // half-configured or entirely-unset WhatsApp setup never affects Telegram or startup.
    public bool IsConfigured { get; }

    public WhatsAppService(
        string? accessToken, string? phoneNumberId, string? appSecret,
        string? verifyToken, string? toNumber,
        string? templateName = null, string? templateLang = null, string apiVersion = "v21.0")
    {
        _appSecret = appSecret;
        _verifyToken = verifyToken;
        _toNumber = toNumber;
        _templateName = templateName ?? "job_search_alert";
        _templateLang = templateLang ?? "en_US";

        IsConfigured = accessToken is not null && phoneNumberId is not null &&
            appSecret is not null && verifyToken is not null && toNumber is not null;

        if (accessToken is not null && phoneNumberId is not null)
        {
            _apiBase = $"https://graph.facebook.com/{apiVersion}/{phoneNumberId}";
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
    }

    // Meta's one-time (and periodic re-) webhook subscription handshake.
    // Returns hub.challenge to echo back on success, null if verification fails.
    public string? HandleVerification(string? mode, string? token, string? challenge)
    {
        if (!IsConfigured) return null;
        if (mode != "subscribe" || token != _verifyToken) return null;
        return challenge;
    }

    public bool VerifySignature(byte[] rawBody, string? signatureHeader)
    {
        if (!IsConfigured || signatureHeader is null) return false;
        if (!signatureHeader.StartsWith("sha256=", StringComparison.Ordinal)) return false;

        var expectedHex = signatureHeader["sha256=".Length..];
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_appSecret!));
        var computedHex = Convert.ToHexString(hmac.ComputeHash(rawBody)).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex), Encoding.UTF8.GetBytes(expectedHex));
    }

    // Returns false if the wamid was already seen (duplicate or concurrent retry).
    public bool TryMarkProcessed(string messageId) =>
        _processed.TryAdd(messageId, 0);

    public void RememberPasteFallback(string wamid, string command, string url) =>
        _pendingPasteFallback[wamid] = (command, url);

    // Consumes the entry so a stale reply to the same prompt can't be reused twice.
    public bool TryGetPasteFallback(string? contextId, out (string Command, string Url) result)
    {
        result = default;
        return contextId is not null && _pendingPasteFallback.TryRemove(contextId, out result);
    }

    public static WhatsAppUpdate? ParseIncoming(JsonElement body)
    {
        try
        {
            if (!body.TryGetProperty("entry", out var entries) || entries.GetArrayLength() == 0)
                return null;
            if (!entries[0].TryGetProperty("changes", out var changes) || changes.GetArrayLength() == 0)
                return null;
            if (!changes[0].TryGetProperty("value", out var value))
                return null;

            // Delivery/read receipts land on the same webhook as a "statuses" array, no "messages".
            if (!value.TryGetProperty("messages", out var messages) || messages.GetArrayLength() == 0)
                return value.TryGetProperty("statuses", out _)
                    ? new WhatsAppUpdate("", null, null, null, true)
                    : null;

            var msg = messages[0];
            var messageId = msg.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
            var from = msg.TryGetProperty("from", out var fromEl) ? fromEl.GetString() : null;

            string? text = null;
            if (msg.TryGetProperty("text", out var textEl) && textEl.TryGetProperty("body", out var bodyEl))
                text = bodyEl.GetString();

            string? contextId = null;
            if (msg.TryGetProperty("context", out var ctx) && ctx.TryGetProperty("id", out var ctxIdEl))
                contextId = ctxIdEl.GetString();

            return new WhatsAppUpdate(messageId, from, text, contextId, false);
        }
        catch
        {
            return null;
        }
    }

    public static string? ExtractUrl(string text)
    {
        var match = Regex.Match(text, @"https?://[^\s<>""]+");
        return match.Success ? match.Value.TrimEnd('.', ',', ')', '>') : null;
    }

    public async Task<string?> SendTemplateAsync(string label, string detail)
    {
        if (!IsConfigured) return null;
        var payload = new
        {
            messaging_product = "whatsapp",
            to = _toNumber,
            type = "template",
            template = new
            {
                name = _templateName,
                language = new { code = _templateLang },
                components = new object[]
                {
                    new
                    {
                        type = "body",
                        parameters = new object[]
                        {
                            new { type = "text", text = label },
                            new { type = "text", text = detail },
                        },
                    },
                },
            },
        };
        return await SendAndExtractIdAsync(payload, "template");
    }

    public async Task<string?> SendTextAsync(string text)
    {
        if (!IsConfigured) return null;
        // WhatsApp's limit is 4096 chars — truncate silently here; use SendChunkedAsync for long content.
        if (text.Length > 4096)
            text = text[..4093] + "...";

        var payload = new
        {
            messaging_product = "whatsapp",
            to = _toNumber,
            type = "text",
            text = new { body = text },
        };
        return await SendAndExtractIdAsync(payload, "text");
    }

    // Splits long output across multiple messages, breaking at newlines where possible.
    public async Task SendChunkedAsync(string text)
    {
        const int Limit = 3800;

        if (text.Length <= Limit)
        {
            await SendTextAsync(text);
            return;
        }

        int start = 0;
        while (start < text.Length)
        {
            int end = Math.Min(start + Limit, text.Length);

            if (end < text.Length)
            {
                int nl = text.LastIndexOf('\n', end - 1, end - start);
                if (nl > start) end = nl + 1;
            }

            await SendTextAsync(text[start..end]);
            start = end;
        }
    }

    // Two-step send: upload to Meta's media endpoint to get a media id, then reference it.
    public async Task<string?> SendDocumentAsync(byte[] fileBytes, string filename, string mimeType = "application/pdf")
    {
        if (!IsConfigured) return null;

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("whatsapp"), "messaging_product");
        form.Add(new StringContent(mimeType), "type");
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        form.Add(fileContent, "file", filename);

        var mediaResp = await _http.PostAsync($"{_apiBase}/media", form);
        if (!mediaResp.IsSuccessStatusCode)
        {
            await LogErrorAsync("media upload", mediaResp);
            return null;
        }

        using var mediaDoc = JsonDocument.Parse(await mediaResp.Content.ReadAsStringAsync());
        var mediaId = mediaDoc.RootElement.GetProperty("id").GetString();
        if (mediaId is null) return null;

        var payload = new
        {
            messaging_product = "whatsapp",
            to = _toNumber,
            type = "document",
            document = new { id = mediaId, filename },
        };
        return await SendAndExtractIdAsync(payload, "document");
    }

    private async Task<string?> SendAndExtractIdAsync(object payload, string kind)
    {
        var resp = await _http.PostAsJsonAsync($"{_apiBase}/messages", payload);
        if (!resp.IsSuccessStatusCode)
        {
            await LogErrorAsync(kind, resp);
            return null;
        }

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var messages = doc.RootElement.GetProperty("messages");
        return messages.GetArrayLength() > 0 ? messages[0].GetProperty("id").GetString() : null;
    }

    private static async Task LogErrorAsync(string kind, HttpResponseMessage resp)
    {
        var body = await resp.Content.ReadAsStringAsync();
        await Console.Error.WriteLineAsync($"[WhatsApp] {kind} {(int)resp.StatusCode}: {body}");
    }
}
