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

    // Idempotent — safe to call on every status poll once verified. Returns whether it
    // actually created a new filter (false if one forwarding to this address already existed).
    public async Task<bool> EnsureJobAlertFilterAsync(string refreshToken, string forwardToAddress)
    {
        // Temporary bracketed diagnostic logging — a NullReferenceException is being thrown
        // somewhere in this method against a real Gmail account, but a local repro against a
        // fake client reaches the same call site and throws the expected auth error instead,
        // so the failure is specific to a real response. Remove once the exact statement is
        // identified and fixed.
        var service = BuildService(refreshToken);
        Console.WriteLine("[diag] EnsureJobAlertFilterAsync: service built, calling Filters.List");
        var existing = await service.Users.Settings.Filters.List("me").ExecuteAsync();
        Console.WriteLine($"[diag] EnsureJobAlertFilterAsync: Filters.List returned, Filter count = {existing.Filter?.Count.ToString() ?? "null"}");
        if (HasFilterForwardingTo(existing.Filter, forwardToAddress))
            return false;

        Console.WriteLine("[diag] EnsureJobAlertFilterAsync: no existing filter, calling Filters.Create");
        var filter = new Filter
        {
            Criteria = new FilterCriteria { Query = FilterQuery },
            Action = new FilterAction { Forward = forwardToAddress },
        };
        await service.Users.Settings.Filters.Create(filter, "me").ExecuteAsync();
        Console.WriteLine("[diag] EnsureJobAlertFilterAsync: Filters.Create returned");
        return true;
    }
}
