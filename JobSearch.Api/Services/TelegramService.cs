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

    public (long UpdateId, string? Text) ParseUpdate(JsonElement update)
    {
        var updateId = update.TryGetProperty("update_id", out var idEl)
            ? idEl.GetInt64()
            : -1L;

        string? text = null;
        if (update.TryGetProperty("message", out var msg) &&
            msg.TryGetProperty("text", out var textEl))
        {
            text = textEl.GetString();
        }

        return (updateId, text);
    }

    public static string? ExtractUrl(string text)
    {
        var match = Regex.Match(text, @"https?://[^\s]+");
        return match.Success ? match.Value.TrimEnd('.', ',', ')') : null;
    }

    public async Task SendMessageAsync(string text)
    {
        var payload = JsonSerializer.Serialize(new
        {
            chat_id = _chatId,
            text,
            parse_mode = "HTML",
            disable_web_page_preview = true,
        });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        await _http.PostAsync($"{_apiBase}/sendMessage", content);
    }
}
