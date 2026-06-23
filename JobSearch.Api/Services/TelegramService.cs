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

    public (long UpdateId, string? Text, string? ReplyToText) ParseUpdate(JsonElement update)
    {
        var updateId = update.TryGetProperty("update_id", out var idEl)
            ? idEl.GetInt64()
            : -1L;

        string? text = null;
        string? replyToText = null;

        if (update.TryGetProperty("message", out var msg))
        {
            if (msg.TryGetProperty("text", out var textEl))
                text = textEl.GetString();

            if (msg.TryGetProperty("reply_to_message", out var reply) &&
                reply.TryGetProperty("text", out var replyTextEl))
                replyToText = replyTextEl.GetString();
        }

        return (updateId, text, replyToText);
    }

    public static string? ExtractUrl(string text)
    {
        var match = Regex.Match(text, @"https?://[^\s<>""]+");
        return match.Success ? match.Value.TrimEnd('.', ',', ')', '>') : null;
    }

    public async Task SendMessageAsync(string text, string? parseMode = "HTML")
    {
        // Telegram's limit is 4096 chars — truncate silently here; use SendChunkedAsync for long content.
        if (text.Length > 4096)
            text = text[..4093] + "...";

        object payload = parseMode is not null
            ? new { chat_id = _chatId, text, parse_mode = parseMode, disable_web_page_preview = true }
            : new { chat_id = _chatId, text, disable_web_page_preview = true };

        using var content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        await _http.PostAsync($"{_apiBase}/sendMessage", content);
    }

    // Splits long output across multiple messages, breaking at newlines where possible.
    public async Task SendChunkedAsync(string text, string? parseMode = null)
    {
        const int Limit = 3800;

        if (text.Length <= Limit)
        {
            await SendMessageAsync(text, parseMode);
            return;
        }

        int start = 0;
        while (start < text.Length)
        {
            int end = Math.Min(start + Limit, text.Length);

            // Try to break at a newline rather than mid-sentence.
            if (end < text.Length)
            {
                int nl = text.LastIndexOf('\n', end - 1, end - start);
                if (nl > start) end = nl + 1;
            }

            await SendMessageAsync(text[start..end], parseMode);
            start = end;
        }
    }
}
