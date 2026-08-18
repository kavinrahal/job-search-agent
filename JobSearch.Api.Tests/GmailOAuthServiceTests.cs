using System.Net;
using System.Text;
using JobSearch.Data;

namespace JobSearch.Api.Tests;

public class GmailOAuthServiceTests
{
    private sealed class StubHandler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken _)
        {
            LastRequest = request;
            if (request.Content is not null) await request.Content.LoadIntoBufferAsync();
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static GmailOAuthService Service(StubHandler handler) =>
        new("client-id", "client-secret", "https://api.example.com/api/v1/gmail-oauth/callback", new HttpClient(handler));

    // TC01 — Requesting the filter-mode scope never smuggles in a broader one. Silent
    // failure: the filter mode's whole trust pitch depends on this scope staying narrow.
    [Fact]
    public void BuildAuthorizationUrl_SettingsBasicScope_ExcludesBroaderScopes()
    {
        var service = Service(new StubHandler("{}"));

        var url = service.BuildAuthorizationUrl("some-state", GmailOAuthService.SettingsBasicScope);

        Assert.Contains("scope=https%3A%2F%2Fwww.googleapis.com%2Fauth%2Fgmail.settings.basic", url);
        Assert.DoesNotContain("gmail.readonly", url);
        Assert.DoesNotContain("gmail.modify", url);
    }

    // TC01b — The full-access mode's readonly scope is requested when explicitly asked for,
    // not silently coerced to the narrower scope.
    [Fact]
    public void BuildAuthorizationUrl_ReadonlyScope_RequestsReadonlyNotSettingsBasic()
    {
        var service = Service(new StubHandler("{}"));

        var url = service.BuildAuthorizationUrl("some-state", GmailOAuthService.ReadonlyScope);

        Assert.Contains("scope=https%3A%2F%2Fwww.googleapis.com%2Fauth%2Fgmail.readonly", url);
        Assert.DoesNotContain("gmail.settings.basic", url);
    }

    // TC02 — access_type=offline and prompt=consent are always present, not conditional.
    // Silent failure: without both, Google only returns a refresh token on a user's very
    // first-ever consent — a reconnect later would silently get no refresh token at all.
    [Fact]
    public void BuildAuthorizationUrl_AlwaysForcesOfflineAccessAndConsentPrompt()
    {
        var service = Service(new StubHandler("{}"));

        var url = service.BuildAuthorizationUrl("some-state", GmailOAuthService.SettingsBasicScope);

        Assert.Contains("access_type=offline", url);
        Assert.Contains("prompt=consent", url);
    }

    // TC03 — The caller-supplied state round-trips into the URL unchanged, for CSRF checking.
    [Fact]
    public void BuildAuthorizationUrl_IncludesGivenState()
    {
        var service = Service(new StubHandler("{}"));

        var url = service.BuildAuthorizationUrl("abc123state", GmailOAuthService.SettingsBasicScope);

        Assert.Contains("state=abc123state", url);
    }

    // TC04 — A well-formed token response yields the refresh token.
    [Fact]
    public async Task ExchangeCodeForRefreshTokenAsync_ValidResponse_ReturnsRefreshToken()
    {
        var service = Service(new StubHandler("""{"access_token":"a","refresh_token":"r-123","expires_in":3600}"""));

        var token = await service.ExchangeCodeForRefreshTokenAsync("some-code");

        Assert.Equal("r-123", token);
    }

    // TC05 — A response missing refresh_token throws clearly rather than a downstream null-ref.
    // Silent failure: this is exactly what happens if prompt=consent is ever accidentally
    // dropped — Google returns 200 with no refresh_token on a repeat consent.
    [Fact]
    public async Task ExchangeCodeForRefreshTokenAsync_MissingRefreshToken_ThrowsClearError()
    {
        var service = Service(new StubHandler("""{"access_token":"a","expires_in":3600}"""));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExchangeCodeForRefreshTokenAsync("some-code"));
        Assert.Contains("refresh_token", ex.Message);
    }

    // TC06 — A non-2xx response from Google throws rather than silently returning null/empty.
    [Fact]
    public async Task ExchangeCodeForRefreshTokenAsync_ErrorResponse_Throws()
    {
        var service = Service(new StubHandler("""{"error":"invalid_grant"}""", HttpStatusCode.BadRequest));

        await Assert.ThrowsAsync<HttpRequestException>(() => service.ExchangeCodeForRefreshTokenAsync("bad-code"));
    }

    // TC07 — The exchange request carries the same redirect_uri used to build the
    // authorization URL. Silent failure: Google rejects the exchange if these two don't
    // match byte-for-byte, but only at request time — a mismatch here wouldn't show up
    // until a real user completes the flow in production.
    [Fact]
    public async Task ExchangeCodeForRefreshTokenAsync_SendsConfiguredRedirectUri()
    {
        var handler = new StubHandler("""{"refresh_token":"r"}""");
        var service = Service(handler);

        await service.ExchangeCodeForRefreshTokenAsync("some-code");

        var sentBody = await handler.LastRequest!.Content!.ReadAsStringAsync();
        Assert.Contains(Uri.EscapeDataString("https://api.example.com/api/v1/gmail-oauth/callback"), sentBody);
    }
}
