using Google;
using Google.Apis.Requests;
using JobSearchAgent.Integrations;

namespace JobSearchAgent.Tests;

// Exercises GmailClient's rate-limit retry/backoff in isolation from the real Gmail API — see
// the comment on ExecuteWithRateLimitRetryAsync for the production incident (a 16-day email
// backlog tripped Gmail's per-minute quota and crashed the whole sync, with nothing persisted
// to advance past it on the next retry) this exists to guard against.
public class GmailClientRetryTests
{
    private static GoogleApiException RateLimitException() => new("gmail", "quota exceeded")
    {
        Error = new RequestError
        {
            Errors = [new SingleError { Reason = "rateLimitExceeded" }],
        },
    };

    private static GoogleApiException AuthFailureException() => new("gmail", "invalid_grant")
    {
        Error = new RequestError
        {
            Errors = [new SingleError { Reason = "authError" }],
        },
    };

    [Fact]
    public void IsRateLimited_TrueForRateLimitReason()
    {
        Assert.True(GmailClient.IsRateLimited(RateLimitException()));
    }

    [Fact]
    public void IsRateLimited_FalseForOtherReasons()
    {
        Assert.False(GmailClient.IsRateLimited(AuthFailureException()));
    }

    [Fact]
    public async Task ExecuteWithRateLimitRetryAsync_RetriesUntilSuccess()
    {
        int attempts = 0;
        var result = await GmailClient.ExecuteWithRateLimitRetryAsync(() =>
        {
            attempts++;
            if (attempts < 3) throw RateLimitException();
            return Task.FromResult("done");
        }, maxAttempts: 5, initialDelay: TimeSpan.FromMilliseconds(1));

        Assert.Equal("done", result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task ExecuteWithRateLimitRetryAsync_GivesUpAfterMaxAttempts()
    {
        int attempts = 0;
        await Assert.ThrowsAsync<GoogleApiException>(() =>
            GmailClient.ExecuteWithRateLimitRetryAsync<string>(() =>
            {
                attempts++;
                throw RateLimitException();
            }, maxAttempts: 3, initialDelay: TimeSpan.FromMilliseconds(1)));

        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task ExecuteWithRateLimitRetryAsync_DoesNotRetryNonRateLimitErrors()
    {
        int attempts = 0;
        await Assert.ThrowsAsync<GoogleApiException>(() =>
            GmailClient.ExecuteWithRateLimitRetryAsync<string>(() =>
            {
                attempts++;
                throw AuthFailureException();
            }, maxAttempts: 5, initialDelay: TimeSpan.FromMilliseconds(1)));

        Assert.Equal(1, attempts);
    }
}
