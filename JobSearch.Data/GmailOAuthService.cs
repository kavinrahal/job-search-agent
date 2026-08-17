using System.Text.Json;

namespace JobSearch.Data;

// Manual OAuth 2.0 authorization-code flow against Google, scoped to gmail.settings.basic
// only (filter management — structurally cannot read mail content). Deliberately not the
// ASP.NET Core Google authentication handler already used for sign-in (JobSearch.Api's
// AddGoogle) — that handler authenticates a browser session; this obtains and persists a
// long-lived refresh token for a different, narrower API scope while the user is already
// signed in, which is a different shape of problem entirely.
//
// Reuses the same Google Cloud OAuth client as JobSearchAgent's existing single-user Gmail
// flow (GMAIL_CLIENT_ID/SECRET) — one client can request different scopes on different
// authorization requests, so a second client isn't needed, only a second registered
// redirect URI for this flow's callback.
public class GmailOAuthService
{
#pragma warning disable S1075 // Google's own fixed OAuth endpoints — not configurable paths
    private const string Scope = "https://www.googleapis.com/auth/gmail.settings.basic";
    private const string AuthorizeUrl = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenUrl = "https://oauth2.googleapis.com/token";
#pragma warning restore S1075

    private readonly HttpClient _http;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _redirectUri;

    public GmailOAuthService(string clientId, string clientSecret, string redirectUri)
        : this(clientId, clientSecret, redirectUri, new HttpClient { Timeout = TimeSpan.FromSeconds(15) }) { }

    public GmailOAuthService(string clientId, string clientSecret, string redirectUri, HttpClient http)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
        _redirectUri = redirectUri;
        _http = http;
    }

    // Where to send the browser to start consent. access_type=offline + prompt=consent
    // guarantee a refresh token comes back on every run — Google only issues one on the
    // first-ever consent by default, which would silently break reconnecting later.
    public string BuildAuthorizationUrl(string state)
    {
        var query = new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["redirect_uri"] = _redirectUri,
            ["response_type"] = "code",
            ["scope"] = Scope,
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["state"] = state,
        };
        return $"{AuthorizeUrl}?{string.Join('&', query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"))}";
    }

    // Exchanges the authorization code Google's redirect carried back for a refresh token.
    public async Task<string> ExchangeCodeForRefreshTokenAsync(string code)
    {
        var response = await _http.PostAsync(TokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["redirect_uri"] = _redirectUri,
            ["grant_type"] = "authorization_code",
        }));
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var body = JsonSerializer.Deserialize<JsonElement>(json);
        if (body.TryGetProperty("refresh_token", out var token))
            return token.GetString()!;

        // Missing rather than malformed — shouldn't occur with prompt=consent always set,
        // but surface it clearly rather than a downstream NullReferenceException if it does.
        throw new InvalidOperationException("Google's token response didn't include a refresh_token.");
    }
}
