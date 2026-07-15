using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;

namespace JobSearchAgent.Integrations;

public class WhatsAppNotifier : IDisposable
{
    private readonly HttpClient _http = new();
    private readonly string _apiBase;
    private readonly string _toNumber;
    private readonly string _templateName;
    private readonly string _templateLang;

    public WhatsAppNotifier(
        string accessToken, string phoneNumberId, string toNumber,
        string templateName = "job_search_alert", string templateLang = "en_US", string apiVersion = "v21.0")
    {
        _apiBase = $"https://graph.facebook.com/{apiVersion}/{phoneNumberId}";
        _toNumber = toNumber;
        _templateName = templateName;
        _templateLang = templateLang;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    // Proactive alerts are business-initiated (cron-triggered), so outside a user-opened
    // 24h window they can only be sent as a pre-approved template. Returns the sent
    // message's wamid on success (persisted for reply-threading), null on any failure.
    public virtual async Task<string?> SendTemplateAsync(string label, string detail)
    {
        try
        {
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

            var response = await _http.PostAsJsonAsync($"{_apiBase}/messages", payload);
            if (!response.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var messages = doc.RootElement.GetProperty("messages");
            return messages.GetArrayLength() > 0 ? messages[0].GetProperty("id").GetString() : null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WhatsApp] Failed to send: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing) _http.Dispose();
    }
}
