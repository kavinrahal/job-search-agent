using System.Net.Http.Json;

namespace JobSearchAgent.Integrations;

public class TelegramNotifier : IDisposable
{
    private readonly HttpClient _http = new();
    private readonly string _baseUrl;
    private readonly string _chatId;

    public TelegramNotifier(string botToken, string chatId)
    {
        _baseUrl = $"https://api.telegram.org/bot{botToken}";
        _chatId = chatId;
    }

    public virtual async Task<bool> SendAsync(string message, string? parseMode = null)
    {
        try
        {
            object payload = parseMode is not null
                ? new { chat_id = _chatId, text = message, parse_mode = parseMode, disable_web_page_preview = true }
                : (object)new { chat_id = _chatId, text = message };
            var response = await _http.PostAsJsonAsync($"{_baseUrl}/sendMessage", payload);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Telegram] Failed to send: {ex.Message}");
            return false;
        }
    }

    public void Dispose() => _http.Dispose();
}
