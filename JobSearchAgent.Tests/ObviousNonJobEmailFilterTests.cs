using JobSearchAgent.Workers;

namespace JobSearchAgent.Tests;

// The filter's whole job is a strict asymmetry: a false negative (skipping a real job email)
// is far worse than a false positive (an obviously-junk email still going to Claude, which just
// costs one wasted classification call). Every test here is really testing that asymmetry.
public class ObviousNonJobEmailFilterTests
{
    // TC01 — the actual target: an automated e-commerce notification with no job-search signal
    // anywhere, caught by subject phrase alone (sender domain is generic, not on the never-job
    // list — the phrase has to carry this one).
    [Fact]
    public void IsObviousNonJob_ShippingNotification_IsFiltered()
    {
        var email = Make.Email(
            subject: "Your order has shipped!",
            fromAddress: "shipment-tracking@amazon.com");

        Assert.True(ObviousNonJobEmailFilter.IsObviousNonJob(email));
    }

    // TC02 — unambiguous consumer-social sender domain with a generic subject — domain alone
    // is enough, no subject phrase needed.
    [Fact]
    public void IsObviousNonJob_SocialNotificationDomain_IsFiltered()
    {
        var email = Make.Email(
            subject: "You have new notifications",
            fromAddress: "notification@facebookmail.com");

        Assert.True(ObviousNonJobEmailFilter.IsObviousNonJob(email));
    }

    // TC03 — a subdomain of a never-job domain should still match (real senders almost always
    // use a subdomain, e.g. mailer.facebookmail.com), confirming the check isn't exact-match-only.
    [Fact]
    public void IsObviousNonJob_NeverJobSubdomain_IsFiltered()
    {
        var email = Make.Email(
            subject: "Someone commented on your post",
            fromAddress: "notify@mailer.facebookmail.com");

        Assert.True(ObviousNonJobEmailFilter.IsObviousNonJob(email));
    }

    // TC04 — a real application confirmation whose subject happens to contain the word
    // "confirmation" (the same word used inside the shipping-notification phrase "order
    // confirmation") must not be caught by a same-word coincidence. Only the full phrase
    // "order confirmation" filters, not "confirmation" alone.
    [Fact]
    public void IsObviousNonJob_ApplicationConfirmation_IsNotFiltered()
    {
        var email = Make.Email(
            subject: "Application Confirmation: Backend Engineer at Acme",
            fromAddress: "hr@acmecorp.com");

        Assert.False(ObviousNonJobEmailFilter.IsObviousNonJob(email));
    }

    // TC05 — a real interview invitation from a generic company domain must survive even
    // though "invited"/"invitation" wording surface-resembles social/marketing phrasing.
    [Fact]
    public void IsObviousNonJob_InterviewInvitation_IsNotFiltered()
    {
        var email = Make.Email(
            subject: "You're invited to a technical interview",
            fromAddress: "talent@othercorp.com");

        Assert.False(ObviousNonJobEmailFilter.IsObviousNonJob(email));
    }

    // TC06 — recruiter outreach from a generic domain must never be caught — recruiters and
    // companies send from all kinds of generic domains, so the domain list stays narrow.
    [Fact]
    public void IsObviousNonJob_RecruiterOutreach_IsNotFiltered()
    {
        var email = Make.Email(
            subject: "I came across your profile — opportunity at a great company",
            fromAddress: "recruiter@talentpartners.io");

        Assert.False(ObviousNonJobEmailFilter.IsObviousNonJob(email));
    }

    // TC07 — ambiguous/borderline: a generic newsletter from a non-social, non-ESP domain.
    // Deliberately NOT filtered — "newsletter"/"digest" wording is excluded from the subject
    // pattern specifically because job_alert digests use the same register ("5 new jobs for
    // your saved search" — see skills/email_categories.md). Bias toward sending it to Claude
    // when unsure, not toward filtering it.
    [Fact]
    public void IsObviousNonJob_AmbiguousNewsletter_IsNotFiltered()
    {
        var email = Make.Email(
            subject: "Your Weekly Newsletter",
            fromAddress: "news@somecompany.com");

        Assert.False(ObviousNonJobEmailFilter.IsObviousNonJob(email));
    }

    // TC08 — a job board's own alert digest (job_alert category) must never be filtered —
    // this is the exact case the "no newsletter/digest wording" design decision protects.
    [Fact]
    public void IsObviousNonJob_JobAlertDigest_IsNotFiltered()
    {
        var email = Make.Email(
            subject: "5 new jobs for your saved search: Software Engineer",
            fromAddress: "jobalerts-noreply@linkedin.com");

        Assert.False(ObviousNonJobEmailFilter.IsObviousNonJob(email));
    }
}
