using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JobSearch.Api.Services;

namespace JobSearch.Api.Tests;

public class WhatsAppServiceTests
{
    private const string AppSecret = "app-secret";
    private const string VerifyToken = "verify-token";

    private static WhatsAppService Make() => new(
        accessToken: "access-token",
        phoneNumberId: "123456",
        appSecret: AppSecret,
        verifyToken: VerifyToken,
        toNumber: "+61400000000");

    // -------------------------------------------------------------------------
    // ParseIncoming
    // -------------------------------------------------------------------------

    // TC31 — Plain text message: fields extracted, ContextId null
    [Fact]
    public void ParseIncoming_PlainMessage_FieldsExtractedContextIdNull()
    {
        using var doc = JsonDocument.Parse("""
            {
                "entry": [{
                    "changes": [{
                        "value": {
                            "messages": [{
                                "id": "wamid.ABC",
                                "from": "61400000000",
                                "text": { "body": "/cv https://au.seek.com/job/123" }
                            }]
                        }
                    }]
                }]
            }
            """);

        var update = WhatsAppService.ParseIncoming(doc.RootElement);

        Assert.NotNull(update);
        Assert.Equal("wamid.ABC", update!.MessageId);
        Assert.Equal("61400000000", update.From);
        Assert.Equal("/cv https://au.seek.com/job/123", update.Text);
        Assert.Null(update.ContextId);
        Assert.False(update.IsStatusUpdate);
    }

    // TC32 — Reply to a message: context.id extracted
    [Fact]
    public void ParseIncoming_ReplyMessage_ContextIdExtracted()
    {
        using var doc = JsonDocument.Parse("""
            {
                "entry": [{
                    "changes": [{
                        "value": {
                            "messages": [{
                                "id": "wamid.NEW",
                                "from": "61400000000",
                                "text": { "body": "details please" },
                                "context": { "id": "wamid.ORIGINAL" }
                            }]
                        }
                    }]
                }]
            }
            """);

        var update = WhatsAppService.ParseIncoming(doc.RootElement);

        Assert.Equal("wamid.ORIGINAL", update!.ContextId);
    }

    // TC33 — Status/delivery-receipt payload (no "messages"): IsStatusUpdate true, not misrouted
    // Silent failure: if a receipt payload were parsed as a real message, the handler could
    // try to process an empty/garbage command instead of ignoring it.
    [Fact]
    public void ParseIncoming_StatusOnlyPayload_IsStatusUpdateTrue()
    {
        using var doc = JsonDocument.Parse("""
            {
                "entry": [{
                    "changes": [{
                        "value": {
                            "statuses": [{ "id": "wamid.ABC", "status": "delivered" }]
                        }
                    }]
                }]
            }
            """);

        var update = WhatsAppService.ParseIncoming(doc.RootElement);

        Assert.NotNull(update);
        Assert.True(update!.IsStatusUpdate);
    }

    // TC34 — Malformed payload (missing entry/changes/value): returns null, does not throw
    // Silent failure: if this throws, the webhook 500s and Meta retries indefinitely.
    [Fact]
    public void ParseIncoming_MalformedPayload_ReturnsNullDoesNotThrow()
    {
        using var doc = JsonDocument.Parse("""{"object":"whatsapp_business_account"}""");

        var update = WhatsAppService.ParseIncoming(doc.RootElement);

        Assert.Null(update);
    }

    // TC35 — Message with no text (e.g. an image message): Text is null
    [Fact]
    public void ParseIncoming_MessageWithoutText_TextIsNull()
    {
        using var doc = JsonDocument.Parse("""
            {
                "entry": [{
                    "changes": [{
                        "value": {
                            "messages": [{ "id": "wamid.ABC", "from": "61400000000", "type": "image" }]
                        }
                    }]
                }]
            }
            """);

        var update = WhatsAppService.ParseIncoming(doc.RootElement);

        Assert.Null(update!.Text);
    }

    // -------------------------------------------------------------------------
    // ExtractUrl (identical regex/behavior to TelegramService.ExtractUrl)
    // -------------------------------------------------------------------------

    // TC36 — Bare URL returned unchanged
    [Fact]
    public void ExtractUrl_BareUrl_ReturnedUnchanged()
    {
        var url = WhatsAppService.ExtractUrl("https://au.seek.com/job/92802816");

        Assert.Equal("https://au.seek.com/job/92802816", url);
    }

    // TC37 — Trailing period stripped
    [Fact]
    public void ExtractUrl_TrailingPeriod_Stripped()
    {
        var url = WhatsAppService.ExtractUrl("See https://example.com/job/1.");

        Assert.Equal("https://example.com/job/1", url);
    }

    // TC38 — No URL in string: returns null
    [Fact]
    public void ExtractUrl_NoUrl_ReturnsNull()
    {
        var url = WhatsAppService.ExtractUrl("No link here.");

        Assert.Null(url);
    }

    // -------------------------------------------------------------------------
    // VerifySignature
    // -------------------------------------------------------------------------

    private static string ComputeSignature(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    // TC39 — Correct HMAC-SHA256 signature over the exact body → true
    [Fact]
    public void VerifySignature_CorrectSignature_ReturnsTrue()
    {
        var body = """{"entry":[]}""";
        var signature = ComputeSignature(body, AppSecret);

        Assert.True(Make().VerifySignature(Encoding.UTF8.GetBytes(body), signature));
    }

    // TC40 — Tampered body with a signature computed for different content → false
    [Fact]
    public void VerifySignature_TamperedBody_ReturnsFalse()
    {
        var signature = ComputeSignature("""{"entry":[]}""", AppSecret);
        var tamperedBody = """{"entry":["tampered"]}""";

        Assert.False(Make().VerifySignature(Encoding.UTF8.GetBytes(tamperedBody), signature));
    }

    // TC41 — Signature computed with the wrong secret → false
    [Fact]
    public void VerifySignature_WrongSecret_ReturnsFalse()
    {
        var body = """{"entry":[]}""";
        var signature = ComputeSignature(body, "wrong-secret");

        Assert.False(Make().VerifySignature(Encoding.UTF8.GetBytes(body), signature));
    }

    // TC42 — Missing signature header → false
    [Fact]
    public void VerifySignature_MissingHeader_ReturnsFalse()
    {
        Assert.False(Make().VerifySignature(Encoding.UTF8.GetBytes("{}"), null));
    }

    // -------------------------------------------------------------------------
    // HandleVerification
    // -------------------------------------------------------------------------

    // TC43 — Correct mode + token → echoes challenge back
    [Fact]
    public void HandleVerification_CorrectToken_ReturnsChallenge()
    {
        var result = Make().HandleVerification("subscribe", VerifyToken, "challenge-string");

        Assert.Equal("challenge-string", result);
    }

    // TC44 — Wrong token → null
    [Fact]
    public void HandleVerification_WrongToken_ReturnsNull()
    {
        var result = Make().HandleVerification("subscribe", "wrong-token", "challenge-string");

        Assert.Null(result);
    }

    // TC45 — Missing mode → null
    [Fact]
    public void HandleVerification_MissingMode_ReturnsNull()
    {
        var result = Make().HandleVerification(null, VerifyToken, "challenge-string");

        Assert.Null(result);
    }

    // -------------------------------------------------------------------------
    // TryMarkProcessed
    // -------------------------------------------------------------------------

    // TC46 — New wamid returns true; same wamid on second call returns false
    [Fact]
    public void TryMarkProcessed_NewIdTrueThenDuplicateFalse()
    {
        var svc = Make();

        Assert.True(svc.TryMarkProcessed("wamid.100"));
        Assert.False(svc.TryMarkProcessed("wamid.100"));
    }

    // -------------------------------------------------------------------------
    // RememberPasteFallback / TryGetPasteFallback
    // -------------------------------------------------------------------------

    // TC47 — Remembered wamid resolves to the exact command/url stored against it
    [Fact]
    public void TryGetPasteFallback_RememberedWamid_ReturnsCommandAndUrl()
    {
        var svc = Make();
        svc.RememberPasteFallback("wamid.200", "/cv", "https://au.seek.com/job/93300225");

        var found = svc.TryGetPasteFallback("wamid.200", out var result);

        Assert.True(found);
        Assert.Equal("/cv", result.Command);
        Assert.Equal("https://au.seek.com/job/93300225", result.Url);
    }

    // TC48 — Consumed once: a second lookup of the same wamid returns false
    // Silent failure: if this doesn't consume the entry, a stale reply to the same
    // prompt could regenerate against a URL the user has since moved on from.
    [Fact]
    public void TryGetPasteFallback_SameWamidTwice_SecondCallReturnsFalse()
    {
        var svc = Make();
        svc.RememberPasteFallback("wamid.201", "/letter", "https://au.seek.com/job/1");

        Assert.True(svc.TryGetPasteFallback("wamid.201", out _));
        Assert.False(svc.TryGetPasteFallback("wamid.201", out _));
    }

    // TC49 — Unknown or null contextId returns false
    [Fact]
    public void TryGetPasteFallback_UnknownOrNullContextId_ReturnsFalse()
    {
        var svc = Make();

        Assert.False(svc.TryGetPasteFallback("wamid.never-remembered", out _));
        Assert.False(svc.TryGetPasteFallback(null, out _));
    }
}
