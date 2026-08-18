using System.Text.RegularExpressions;

namespace JobSearch.Data;

// Detects Gmail's own "confirm this forwarding address" email and pulls out the
// verification link so the app can fetch it server-side. The target address is our own
// inbound webhook, not a mailbox anyone can actually check, so nobody could ever click that
// link by hand. Fetching the token-bearing URL is what completes the confirmation, and
// since we already control that inbox, doing that fetch ourselves the moment the email
// arrives is the natural automation, not a way of bypassing anything.
public static class GmailForwardingConfirmation
{
    private const string ExpectedSender = "forwarding-noreply@google.com";

    // "vf-" (verify) specifically, not "uf-" (the cancel-forwarding link Gmail sends in the
    // same family of emails) — matching that instead would silently undo the very thing
    // this is supposed to confirm. Host is hardcoded to Gmail's two actual confirmation
    // domains — confirmed against real emails that the initial add and a "resend
    // confirmation" use different hosts (mail-settings.google.com vs mail.google.com) for
    // the same kind of link — not a generic "anything on google.com" pattern, so a spoofed
    // From header alone still can't point this at an attacker-chosen URL.
    //
    // The token itself is percent-encoded (a real one contains literal "%5B"/"%5D" — encoded
    // square brackets) — confirmed against an actual Gmail confirmation email, not guessed —
    // so the character class has to allow "%" too, not just alphanumerics/hyphen/underscore.
    // Matches everything up to the first whitespace or quote/angle-bracket, which a URL
    // embedded in plain-text or HTML mail never legitimately contains, rather than trying to
    // enumerate every character Google's token format might use.
    private static readonly Regex VerifyLinkPattern = new(
        @"https://(?:mail-settings|mail)\.google\.com/mail/vf-[^\s""'<>]+",
        RegexOptions.Compiled);

    public static bool TryExtractVerificationLink(string from, string bodyText, out string link)
    {
        link = "";
        if (!from.Contains(ExpectedSender, StringComparison.OrdinalIgnoreCase))
            return false;

        var match = VerifyLinkPattern.Match(bodyText);
        if (!match.Success)
            return false;

        link = match.Value;
        return true;
    }
}
