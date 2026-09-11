using System.Text.RegularExpressions;
using JobSearchAgent.Agents;
using JobSearchAgent.Models;

namespace JobSearchAgent.Workers;

// Cheap deterministic pre-filter that runs before EmailClassifier's Claude call, for full-mode
// (gmail.readonly) users only. Filter-tracking-mode users already get a real pre-filter for
// free — a Gmail-side filter rule (GmailSettingsClient.AcknowledgmentFilterQuery /
// JobAlertFilterQuery) plus SendGrid inbound forwarding, so only already-matched mail ever
// reaches this app at all. Full-mode users read their whole inbox directly and had no
// equivalent — every fetched email, including obvious newsletters/receipts/social
// notifications, paid for a full Claude classification call. This closes that gap with the
// same "deterministic filter before spending on the model" principle, not a replacement for
// classification.
//
// Bias, deliberately: a false negative (silently skipping a real job email) is far worse than
// a false positive (sending something irrelevant to Claude, which just costs one wasted call).
// Every signal below is narrow and only fires on strong, unambiguous senders/phrasing — anything
// else falls through to Claude, same spirit as the classifier's own "when in doubt,
// not_relevant" instruction (see EmailClassifier's system prompt).
public static class ObviousNonJobEmailFilter
{
    // Sender domains that are exclusively consumer social/notification platforms — never used
    // for job applications, recruiter outreach, or job board digests in practice. Deliberately
    // excludes generic marketing ESPs (Mailchimp, Constant Contact, etc.) since a recruiting
    // agency's newsletter can legitimately go through one of those, and deliberately excludes
    // job-adjacent platforms (LinkedIn, Indeed, Seek, Workday, greenhouse, etc.) even though
    // they also send plenty of non-job mail — those senders are genuinely used for job-related
    // email too (see GmailSettingsClient.KnownAckDomains), so domain alone isn't a safe signal
    // for them the way it is for something like facebookmail.com.
    private static readonly string[] NeverJobDomains =
    [
        "facebookmail.com", "facebook.com", "instagram.com", "pinterest.com", "e.pinterest.com",
        "twitter.com", "t.email.twitter.com", "x.com", "discord.com", "discordapp.com",
        "snapchat.com", "tiktok.com", "nextdoor.com",
    ];

    // Subject phrases strong enough on their own to be an obvious automated notification.
    // Deliberately multi-word phrases only — never a single generic word ("confirmation",
    // "receipt", "invoice", "invitation" alone would false-positive on real job emails like
    // "Application Confirmation" or "Interview Invitation" — deliberately excludes
    // "newsletter"/"digest"-style wording too: job_alert digests ("5 new jobs for your saved
    // search") use exactly that register, so it's not a safe signal here — see
    // skills/email_categories.md's job_alert examples.
    private static readonly string[] ObviousSubjectPhrases =
    [
        "your order has shipped", "order confirmation", "shipping confirmation",
        "your receipt from", "your receipt for", "payment receipt", "your invoice is ready",
        "out for delivery", "delivery confirmation", "tracking number for your order",
        "your order is on its way", "your subscription has renewed",
        "your subscription is expiring", "auto-renewal notice", "your monthly statement",
        "statement is ready",
        "new follower", "friend request", "tagged you in", "commented on your",
        "liked your photo", "liked your post", "mentioned you in a", "added you as a friend",
        "sent you a friend request",
    ];

    private static readonly Regex ObviousSubjectPattern = new(
        @"\b(" + string.Join("|", ObviousSubjectPhrases.Select(Regex.Escape)) + @")\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsObviousNonJob(RawEmail email)
    {
        string? domain = ApplicationTracker.ExtractDomain(email.FromAddress);
        if (domain is not null && IsNeverJobDomain(domain))
            return true;

        return ObviousSubjectPattern.IsMatch(email.Subject ?? "");
    }

    private static bool IsNeverJobDomain(string domain) =>
        NeverJobDomains.Any(d =>
            domain.Equals(d, StringComparison.OrdinalIgnoreCase) ||
            domain.EndsWith("." + d, StringComparison.OrdinalIgnoreCase));
}
