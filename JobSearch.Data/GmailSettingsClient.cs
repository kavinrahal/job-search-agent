using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using System.Net;

namespace JobSearch.Data;

public static class GmailForwardingStatus
{
    public const string NotAdded = "not_added";
    public const string Pending = "pending";
    public const string Verified = "verified";
}

// Gmail Settings API access for the Tier2 alert-forwarding flow — deliberately separate
// from JobSearchAgent/Integrations/GmailClient.cs, which is a different, pre-existing
// single-user pipeline scoped to gmail.readonly. This one only ever touches Settings
// endpoints (filters, forwarding addresses), matching the narrower gmail.settings.basic
// scope GmailOAuthService actually obtained consent for — calling anything outside that
// scope would fail with a 403 from Google regardless of what's declared client-side, so
// there's no risk of this accidentally reading mail content.
//
// Gmail's forwardingAddresses.create is restricted to domain-wide-delegated service
// accounts and does not work for a personal Gmail account under normal per-user OAuth
// (confirmed against Google's own reference docs) — so this class deliberately has no
// "create forwarding address" method. The user adds and confirms that address themselves
// in Gmail's own settings UI; this class only reads that status back and, once verified,
// installs the actual filter.
public class GmailSettingsClient
{
    private readonly string _clientId;
    private readonly string _clientSecret;

    public GmailSettingsClient(string clientId, string clientSecret)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
    }

    private GmailService BuildService(string refreshToken)
    {
        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = _clientId, ClientSecret = _clientSecret },
            Scopes = [GmailService.Scope.GmailSettingsBasic],
        });
        var credential = new UserCredential(flow, "user", new TokenResponse { RefreshToken = refreshToken });
        return new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "JobSearchAgent",
        });
    }

    public async Task<string> GetForwardingStatusAsync(string refreshToken, string address)
    {
        var service = BuildService(refreshToken);
        try
        {
            var forwarding = await service.Users.Settings.ForwardingAddresses.Get("me", address).ExecuteAsync();
            return forwarding.VerificationStatus == "accepted" ? GmailForwardingStatus.Verified : GmailForwardingStatus.Pending;
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            return GmailForwardingStatus.NotAdded;
        }
    }

    // Only the sender/subject patterns most job boards' alert emails actually use — a
    // first-pass heuristic, not an exhaustive list. Easy to extend once real forwarded mail
    // shows what's being missed.
    public const string FilterQuery =
        "from:(jobalerts-noreply@linkedin.com OR jobs-noreply@linkedin.com OR alerts@seek.com.au " +
        "OR jobalert@indeed.com OR alert@indeed.com OR noreply@jora.com OR noreply@adzuna.com.au) " +
        "OR subject:(\"job alert\" OR \"jobs for you\" OR \"new jobs matching\" OR \"jobs matching your search\")";

    // Pure decision logic, split out so it's testable without faking the Gmail SDK's HTTP
    // transport — the thing actually worth guarding against is silently creating duplicate
    // filters on every poll.
    public static bool HasFilterForwardingTo(IEnumerable<Filter>? filters, string address) =>
        filters?.Any(f => f.Action?.Forward == address) == true;

    // Matches on query too, not just forward address — needed once a user can have more than
    // one filter forwarding to the same address (job-alert filter + acknowledgment filter).
    public static bool HasFilterForQuery(IEnumerable<Filter>? filters, string query, string address) =>
        filters?.Any(f => f.Action?.Forward == address && f.Criteria?.Query == query) == true;

    // Idempotent — safe to call on every status poll once verified. Returns whether it
    // actually created a new filter (false if one already existed). Shared by both filter
    // kinds this app installs (the fixed job-alert filter and per-company ones) — only the
    // query and the existence check differ between them.
    private async Task<bool> EnsureFilterAsync(
        string refreshToken, string query, string forwardToAddress, Func<IEnumerable<Filter>?, bool> alreadyExists)
    {
        var service = BuildService(refreshToken);
        var existing = await service.Users.Settings.Filters.List("me").ExecuteAsync();
        // Gmail's API returns an empty response body (not an object with an empty list) when
        // the account has zero filters, which the client library deserializes as a null
        // ListFiltersResponse rather than one with a null/empty Filter property — confirmed
        // against a real freshly-authorized account. `existing?.Filter` (not `existing.Filter`)
        // is required here, not just on the nested property.
        if (alreadyExists(existing?.Filter))
            return false;

        var filter = new Filter
        {
            Criteria = new FilterCriteria { Query = query },
            Action = new FilterAction { Forward = forwardToAddress },
        };
        await service.Users.Settings.Filters.Create(filter, "me").ExecuteAsync();
        return true;
    }

    public Task<bool> EnsureJobAlertFilterAsync(string refreshToken, string forwardToAddress) =>
        EnsureFilterAsync(refreshToken, FilterQuery, forwardToAddress,
            filters => HasFilterForwardingTo(filters, forwardToAddress));

    // The filter-only tracking mode's mechanism: forwards mail from one company's domain,
    // installed the moment a user logs an application with a CompanyDomain (see
    // POST /applications). Unlike the job-alert filter there can be many of these per user
    // (one per tracked company), so idempotency has to key on the domain too — not just the
    // forward address, which every filter this app installs shares.
    public static string CompanyFilterQuery(string companyDomain) => $"from:(*@{companyDomain})";

    public static bool HasCompanyFilter(IEnumerable<Filter>? filters, string companyDomain, string address) =>
        HasFilterForQuery(filters, CompanyFilterQuery(companyDomain), address);

    public Task<bool> EnsureCompanyFilterAsync(string refreshToken, string companyDomain, string forwardToAddress) =>
        EnsureFilterAsync(refreshToken, CompanyFilterQuery(companyDomain), forwardToAddress,
            filters => HasCompanyFilter(filters, companyDomain, forwardToAddress));

    // Sender domains for the ~20 ATS/job-board platforms that account for the large majority
    // of real application-acknowledgment traffic (derived by analyzing 197 of the account
    // owner's own real acknowledgment emails — see the forwarding-strategy discussion this
    // shipped with). Exposed publicly so AcknowledgmentDomainCapture can skip auto-installing
    // a redundant per-domain filter for a sender already covered here.
    public static readonly IReadOnlySet<string> KnownAckDomains = new HashSet<string>
    {
        "s.seek.com.au", "linkedin.com", "indeed.com", "smartrecruiters.com",
        "adzuna.com.au", "app.bamboohr.com", "employmenthero.com", "candidates.workablemail.com",
        "us.greenhouse-mail.io", "myworkday.com", "otp.workday.com", "hire.lever.co",
        "livehire.com", "broadbean.net", "jobadder.com", "send.dover.com",
        "au.notification.hays.com", "successfactors.com", "mail.pageuppeople.com", "ashbyhq.com",
    };

    // Direct company/agency senders (their own domain, not a shared ATS platform) don't share
    // a sender domain worth pre-populating — this phrase list is what catches those instead.
    // Subject-only, not body text: keeps the filter precise and matches the existing
    // job-alert filter's own subject:(...) group rather than introducing a new matching style.
    // Built from KnownAckDomains rather than a second hand-typed list — one list to keep
    // in sync with AcknowledgmentDomainCapture's "already covered" check, not two.
    public static readonly string AcknowledgmentFilterQuery =
        $"from:(*@{string.Join(" OR *@", KnownAckDomains)}) " +
        "OR subject:(\"successfully submitted\" OR \"application submitted\" OR \"thank you for applying\" " +
        "OR \"thanks for applying\" OR \"thank you for your application\" OR \"received your application\" " +
        "OR \"application confirmation\" OR \"track your application\" OR \"application received\" " +
        "OR \"thank you for submitting your job application\")";

    public Task<bool> EnsureAcknowledgmentFilterAsync(string refreshToken, string forwardToAddress) =>
        EnsureFilterAsync(refreshToken, AcknowledgmentFilterQuery, forwardToAddress,
            filters => HasFilterForQuery(filters, AcknowledgmentFilterQuery, forwardToAddress));
}
