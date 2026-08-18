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
    // this is supposed to confirm. Host is hardcoded to Gmail's actual confirmation domain,
    // not a generic "anything on google.com" pattern, so a spoofed From header alone still
    // can't point this at an attacker-chosen URL.
    private static readonly Regex VerifyLinkPattern = new(
        @"https://mail-settings\.google\.com/mail/vf-[A-Za-z0-9_-]+",
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
