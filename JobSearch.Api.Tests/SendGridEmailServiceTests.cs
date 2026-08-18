using System.Net;
using System.Text.Json;
using JobSearch.Data;

namespace JobSearch.Api.Tests;

public class SendGridEmailServiceTests
{
    private sealed class StubHandler(HttpStatusCode status = HttpStatusCode.Accepted) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken _)
        {
            LastRequest = request;
            if (request.Content is not null) await request.Content.LoadIntoBufferAsync();
            return new HttpResponseMessage(status);
        }
    }

    // TC01 — A successful call sends the expected recipient, sender, subject, and body.
    [Fact]
    public async Task SendAsync_ValidCall_SendsExpectedPayload()
    {
        var handler = new StubHandler();
        var service = new SendGridEmailService("test-key", "invites@example.com", new HttpClient(handler));

        await service.SendAsync("someone@example.com", "You're invited", "Welcome to the beta.");

        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        Assert.Equal("someone@example.com",
            root.GetProperty("personalizations")[0].GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Equal("invites@example.com", root.GetProperty("from").GetProperty("email").GetString());
        Assert.Equal("You're invited", root.GetProperty("subject").GetString());
        Assert.Equal("Welcome to the beta.", root.GetProperty("content")[0].GetProperty("value").GetString());
    }

    // TC02 — The API key is sent as a Bearer token, not embedded in the body or query string.
    [Fact]
    public async Task SendAsync_ValidCall_SendsApiKeyAsBearerToken()
    {
        var handler = new StubHandler();
        var service = new SendGridEmailService("my-secret-key", "invites@example.com", new HttpClient(handler));

        await service.SendAsync("someone@example.com", "Subject", "Body");

        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("my-secret-key", handler.LastRequest.Headers.Authorization.Parameter);
    }

    // TC03 — A non-2xx response from SendGrid throws rather than silently swallowing the
    // failure — a caller needs to know an invite email didn't actually go out.
    [Fact]
    public async Task SendAsync_ErrorResponse_Throws()
    {
        var handler = new StubHandler(HttpStatusCode.Unauthorized);
        var service = new SendGridEmailService("bad-key", "invites@example.com", new HttpClient(handler));

        await Assert.ThrowsAsync<HttpRequestException>(() => service.SendAsync("someone@example.com", "Subject", "Body"));
    }
}
