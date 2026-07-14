using System.Text.Json;
using JobSearch.Api.Services;

namespace JobSearch.Api.Tests;

public class TelegramServiceTests
{
    private static TelegramService Make() => new("bot-token", "webhook-secret", "chat-id");

    // -------------------------------------------------------------------------
    // ParseUpdate
    // -------------------------------------------------------------------------

    // TC19 — Plain command message: text extracted, no replyToText
    [Fact]
    public void ParseUpdate_PlainMessage_TextExtractedReplyToNull()
    {
        var svc = Make();
        using var doc = JsonDocument.Parse("""
            {"update_id":1,"message":{"text":"/cv https://au.seek.com/job/123"}}
            """);

        var (updateId, text, replyToText) = TelegramService.ParseUpdate(doc.RootElement);

        Assert.Equal(1L, updateId);
        Assert.Equal("/cv https://au.seek.com/job/123", text);
        Assert.Null(replyToText);
    }

    // TC20 — Reply-to message: both text and replyToText extracted
    // Silent failure: missing the reply_to_message path means /letter with a replied notification
    // has no URL to look up, silently responds "no URL found."
    [Fact]
    public void ParseUpdate_ReplyToMessage_BothTextsExtracted()
    {
        var svc = Make();
        using var doc = JsonDocument.Parse("""
            {
                "update_id": 2,
                "message": {
                    "text": "/letter",
                    "reply_to_message": {
                        "text": "Canva — Engineer\nhttps://au.seek.com/job/456"
                    }
                }
            }
            """);

        var (_, text, replyToText) = TelegramService.ParseUpdate(doc.RootElement);

        Assert.Equal("/letter", text);
        Assert.Equal("Canva — Engineer\nhttps://au.seek.com/job/456", replyToText);
    }

    // TC21 — No message property (e.g. channel_post): text/replyToText are null, updateId still read
    // Silent failure: if missing message throws, webhook handler crashes and Telegram retries forever.
    [Fact]
    public void ParseUpdate_NoMessageProperty_TextNullUpdateIdStillExtracted()
    {
        var svc = Make();
        using var doc = JsonDocument.Parse("""{"update_id":3}""");

        var (updateId, text, replyToText) = TelegramService.ParseUpdate(doc.RootElement);

        Assert.Equal(3L, updateId);
        Assert.Null(text);
        Assert.Null(replyToText);
    }

    // TC22 — Message with no text property (e.g. photo message): text is null
    [Fact]
    public void ParseUpdate_MessageWithoutText_TextIsNull()
    {
        var svc = Make();
        using var doc = JsonDocument.Parse("""{"update_id":4,"message":{"photo":{}}}""");

        var (_, text, _) = TelegramService.ParseUpdate(doc.RootElement);

        Assert.Null(text);
    }

    // -------------------------------------------------------------------------
    // ExtractUrl
    // -------------------------------------------------------------------------

    // TC23 — Bare URL returned unchanged
    [Fact]
    public void ExtractUrl_BareUrl_ReturnedUnchanged()
    {
        var url = TelegramService.ExtractUrl("https://au.seek.com/job/92802816");

        Assert.Equal("https://au.seek.com/job/92802816", url);
    }

    // TC24 — Trailing period stripped (common when URL appears at end of sentence)
    [Fact]
    public void ExtractUrl_TrailingPeriod_Stripped()
    {
        var url = TelegramService.ExtractUrl("See https://example.com/job/1.");

        Assert.Equal("https://example.com/job/1", url);
    }

    // TC25 — URL inside href="..." attribute: regex boundary on " stops at attribute close
    // Silent failure: if the pattern consumed the closing quote, the URL would include `">View posting`
    // which would fail DB lookup.
    [Fact]
    public void ExtractUrl_InsideHrefAttribute_StopsBeforeClosingQuote()
    {
        var url = TelegramService.ExtractUrl("""<a href="https://au.seek.com/job/789">View posting</a>""");

        Assert.Equal("https://au.seek.com/job/789", url);
    }

    // TC26 — No URL in string: returns null
    [Fact]
    public void ExtractUrl_NoUrl_ReturnsNull()
    {
        var url = TelegramService.ExtractUrl("No link here.");

        Assert.Null(url);
    }

    // -------------------------------------------------------------------------
    // VerifySecretToken
    // -------------------------------------------------------------------------

    // TC27 — Correct token returns true
    [Fact]
    public void VerifySecretToken_CorrectToken_ReturnsTrue()
    {
        Assert.True(Make().VerifySecretToken("webhook-secret"));
    }

    // TC28 — Wrong token returns false
    [Fact]
    public void VerifySecretToken_WrongToken_ReturnsFalse()
    {
        Assert.False(Make().VerifySecretToken("wrong"));
    }

    // TC29 — Empty string returns false (not accidentally equal to empty secret)
    [Fact]
    public void VerifySecretToken_EmptyString_ReturnsFalse()
    {
        Assert.False(Make().VerifySecretToken(""));
    }

    // -------------------------------------------------------------------------
    // TryMarkProcessed
    // -------------------------------------------------------------------------

    // TC30 — New ID returns true; same ID on second call returns false
    // Silent failure: if the dedup set is not per-instance, concurrent webhook retries
    // could process the same Telegram update twice (e.g. double-send on /letter).
    [Fact]
    public void TryMarkProcessed_NewIdTrueThenDuplicateFalse()
    {
        var svc = Make();

        Assert.True(svc.TryMarkProcessed(100L));
        Assert.False(svc.TryMarkProcessed(100L));
    }
}
