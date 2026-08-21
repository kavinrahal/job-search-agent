using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace JobSearch.Data;

// Plain HTTP call against SendGrid's Mail Send API — separate from the Inbound Parse
// pipeline (InboundEmailService), which only ever receives mail, never sends it. Uses a
// distinct SendGrid API key (Mail Send permission) from the inbound webhook's shared secret.
public class SendGridEmailService
{
#pragma warning disable S1075 // SendGrid's own fixed API endpoint — not a configurable path
    private const string SendUrl = "https://api.sendgrid.com/v3/mail/send";
#pragma warning restore S1075

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _fromEmail;
    private readonly string _fromName;

    // A bare "invites@worksanta.com" with no display name is itself a small spam/trust
    // signal on top of domain authentication — a named sender reads as a real product to
    // both filters and humans.
    public SendGridEmailService(string apiKey, string fromEmail, string fromName = "Work Santa")
        : this(apiKey, fromEmail, fromName, new HttpClient { Timeout = TimeSpan.FromSeconds(15) }) { }

    public SendGridEmailService(string apiKey, string fromEmail, string fromName, HttpClient http)
    {
        _apiKey = apiKey;
        _fromEmail = fromEmail;
        _fromName = fromName;
        _http = http;
    }

    public async Task SendAsync(string toEmail, string subject, string bodyText)
    {
        var payload = new
        {
            personalizations = new[] { new { to = new[] { new { email = toEmail } } } },
            from = new { email = _fromEmail, name = _fromName },
            subject,
            content = new[] { new { type = "text/plain", value = bodyText } },
            // SendGrid's account-wide default rewrites every link in the body through a
            // branded urlNNNN.worksanta.com tracking redirect. That subdomain's certificate
            // isn't valid yet on SendGrid's side, which breaks the invite link itself —
            // Chrome hard-blocks it on mobile (HSTS) and shows "not secure" on desktop.
            // These emails don't need click analytics; a plain, unmodified link is also
            // more trustworthy to both spam filters and the person reading it.
            tracking_settings = new { click_tracking = new { enable = false } },
        };

        var request = new HttpRequestMessage(HttpMethod.Post, SendUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}
