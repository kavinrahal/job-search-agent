using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JobSearch.Api.Services;

public class TelegramService
{
    private readonly HttpClient _http = new();
    private readonly string _apiBase;
    private readonly string _chatId;
    private readonly string _secretToken;

    // Tracks processed update_ids to prevent duplicate processing on Telegram retries.
    private readonly ConcurrentDictionary<long, byte> _processed = new();

    public TelegramService(string botToken, string secretToken, string chatId)
    {
        _apiBase = $"https://api.telegram.org/bot{botToken}";
        _secretToken = secretToken;
        _chatId = chatId;
    }

    public bool VerifySecretToken(string headerValue) =>
        string.Equals(headerValue, _secretToken, StringComparison.Ordinal);

    // Returns false if the update_id was already seen (duplicate or concurrent retry).
    public bool TryMarkProcessed(long updateId) =>
        _processed.TryAdd(updateId, 0);

    public static (long UpdateId, string? Text, string? ReplyToText, string? ReplyToMessageId) ParseUpdate(JsonElement update)
    {
        var updateId = update.TryGetProperty("update_id", out var idEl)
            ? idEl.GetInt64()
            : -1L;

        string? text = null;
        string? replyToText = null;
        string? replyToMessageId = null;

        if (update.TryGetProperty("message", out var msg))
        {
            if (msg.TryGetProperty("text", out var textEl))
                text = textEl.GetString();

            if (msg.TryGetProperty("reply_to_message", out var reply))
            {
                if (reply.TryGetProperty("text", out var replyTextEl))
                    replyToText = replyTextEl.GetString();

                if (reply.TryGetProperty("message_id", out var replyIdEl))
                    replyToMessageId = replyIdEl.GetInt64().ToString();
            }
        }

        return (updateId, text, replyToText, replyToMessageId);
    }

    public static string? ExtractUrl(string text)
    {
        var match = Regex.Match(text, @"https?://[^\s<>""]+");
        return match.Success ? match.Value.TrimEnd('.', ',', ')', '>') : null;
    }

    // Returns the sent message's message_id (as a string, for reply-threading lookups), or null on failure.
    public async Task<string?> SendMessageAsync(string text, string? parseMode = "HTML")
    {
        // Telegram's limit is 4096 chars — truncate silently here; use SendChunkedAsync for long content.
        if (text.Length > 4096)
            text = text[..4093] + "...";

        object payload = parseMode is not null
            ? new { chat_id = _chatId, text, parse_mode = parseMode, disable_web_page_preview = true }
            : new { chat_id = _chatId, text, disable_web_page_preview = true };

        using var content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync($"{_apiBase}/sendMessage", content);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            await Console.Error.WriteLineAsync($"[Telegram] sendMessage {(int)resp.StatusCode}: {body}");
            return null;
        }

        return ExtractMessageId(body);
    }

    public async Task<string?> SendDocumentAsync(byte[] fileBytes, string filename)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(_chatId), "chat_id");
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "document", filename);

        var resp = await _http.PostAsync($"{_apiBase}/sendDocument", form);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            await Console.Error.WriteLineAsync($"[Telegram] sendDocument {(int)resp.StatusCode}: {body}");
            return null;
        }

        return ExtractMessageId(body);
    }

    // Splits long output across multiple messages, breaking at newlines where possible.
    // Returns the message_id of the last chunk sent, since that's the message a reply naturally targets.
    public async Task<string?> SendChunkedAsync(string text, string? parseMode = null)
    {
        const int Limit = 3800;

        if (text.Length <= Limit)
            return await SendMessageAsync(text, parseMode);

        int start = 0;
        string? lastMessageId = null;
        while (start < text.Length)
        {
            int end = Math.Min(start + Limit, text.Length);

            // Try to break at a newline rather than mid-sentence.
            if (end < text.Length)
            {
                int nl = text.LastIndexOf('\n', end - 1, end - start);
                if (nl > start) end = nl + 1;
            }

            lastMessageId = await SendMessageAsync(text[start..end], parseMode);
            start = end;
        }

        return lastMessageId;
    }

    private static string? ExtractMessageId(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            return doc.RootElement.TryGetProperty("result", out var result) &&
                   result.TryGetProperty("message_id", out var idEl)
                ? idEl.GetInt64().ToString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
