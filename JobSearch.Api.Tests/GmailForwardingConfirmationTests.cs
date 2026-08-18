using JobSearch.Data;

namespace JobSearch.Api.Tests;

public class GmailForwardingConfirmationTests
{
    private const string RealSender = "Gmail Team <forwarding-noreply@google.com>";
    private const string VerifyLink = "https://mail-settings.google.com/mail/vf-AbC123_xyz-9";

    // TC01 — The actual shape of a real Gmail confirmation email: matching sender, verify
    // link present somewhere in a longer plain-text body.
    [Fact]
    public void TryExtractVerificationLink_RealConfirmationEmail_ExtractsLink()
    {
        var body = $"""
            Hi,

            You recently added abc123@alerts.worksanta.com as a forwarding address.

            To confirm this is correct, click here: {VerifyLink}

            If you didn't request this, ignore this email.
            """;

        var found = GmailForwardingConfirmation.TryExtractVerificationLink(RealSender, body, out var link);

        Assert.True(found);
        Assert.Equal(VerifyLink, link);
    }

    // TC02 — Silent failure this guards against: matching the cancel link ("uf-") instead
    // of the verify link ("vf-") would undo the very thing this is supposed to confirm.
    [Fact]
    public void TryExtractVerificationLink_OnlyCancelLinkPresent_ReturnsFalse()
    {
        var body = "To stop this forwarding, click here: https://mail-settings.google.com/mail/uf-AbC123_xyz-9";

        var found = GmailForwardingConfirmation.TryExtractVerificationLink(RealSender, body, out _);

        Assert.False(found);
    }

    // TC03 — A spoofed From header claiming to be Google doesn't extract anything on its
    // own — the sender string here isn't Google's actual address, just similar-looking text.
    [Fact]
    public void TryExtractVerificationLink_SpoofedSender_ReturnsFalse()
    {
        var spoofedFrom = "Gmail Team <attacker@evil.example.com>";

        var found = GmailForwardingConfirmation.TryExtractVerificationLink(spoofedFrom, $"Click here: {VerifyLink}", out _);

        Assert.False(found);
    }

    // TC04 — Correct sender but a link on a different host doesn't match, even if it
    // superficially resembles the real one (e.g. a lookalike domain).
    [Fact]
    public void TryExtractVerificationLink_LinkOnWrongHost_ReturnsFalse()
    {
        var body = "Click here: https://mail-settings.google.com.evil.example.com/mail/vf-AbC123";

        var found = GmailForwardingConfirmation.TryExtractVerificationLink(RealSender, body, out _);

        Assert.False(found);
    }

    // TC05 — An ordinary forwarded job-alert email (the overwhelmingly common case) never
    // matches, regardless of what URLs happen to be in it.
    [Fact]
    public void TryExtractVerificationLink_UnrelatedEmail_ReturnsFalse()
    {
        var found = GmailForwardingConfirmation.TryExtractVerificationLink(
            "LinkedIn Job Alerts <jobalerts-noreply@linkedin.com>",
            "New job: https://www.linkedin.com/jobs/view/12345",
            out _);

        Assert.False(found);
    }

    // TC06 — Regression test for the actual bug this shipped with: a real Gmail
    // confirmation email's token contains percent-encoded brackets ("%5B"/"%5D"), which an
    // alphanumeric-only character class doesn't match at all, so the very first "%" right
    // after "vf-" made the whole pattern fail silently. Body below is the real structure
    // captured from a live confirmation email (token shortened, not the actual value).
    [Fact]
    public void TryExtractVerificationLink_RealBodyWithPercentEncodedToken_ExtractsFullLink()
    {
        const string realLink =
            "https://mail-settings.google.com/mail/vf-%5BANGjdJ-7qe9-EB0iP-VcIGZFKzwUrB7DCIY2DQAhJ%5D-oqWoRDa0qM8RBcmgZ9u8YDg3-5U";
        var body = $"""
            kavinrahal@gmail.com has requested to automatically forward mail to your email
            address abc123@alerts.worksanta.com.

            If you do not approve of this request, no further action is required.

            To allow kavinrahal@gmail.com to automatically forward mail to your address,
            please click the link below to confirm the request:

            {realLink}

            If you click the link and it appears to be broken, please copy and paste it
            into a new browser window.
            """;

        var found = GmailForwardingConfirmation.TryExtractVerificationLink(RealSender, body, out var link);

        Assert.True(found);
        Assert.Equal(realLink, link);
    }

    // TC07 — Regression test for a second real-world bug: a "resend confirmation" email
    // uses a different (but equally real) Gmail host for the same kind of link —
    // mail.google.com instead of mail-settings.google.com — confirmed against a live resend
    // (token shape below is synthetic, not the actual captured value).
    [Fact]
    public void TryExtractVerificationLink_ResendHostVariant_ExtractsLink()
    {
        const string resendLink =
            "https://mail.google.com/mail/vf-%5BANGjdJ_exampleTokenShapeOnly_NotARealToken%5D-abc123";
        var body = $"user@example.com has requested to automatically forward mail. Click here: {resendLink}";

        var found = GmailForwardingConfirmation.TryExtractVerificationLink(RealSender, body, out var link);

        Assert.True(found);
        Assert.Equal(resendLink, link);
    }
}
