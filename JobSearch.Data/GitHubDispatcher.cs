using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace JobSearch.Data;

// Fires a GitHub repository_dispatch event, which is what actually starts the crash-fix
// workflow.
//
// This relay exists because Sentry can't call GitHub directly: repository_dispatch requires
// an Authorization header, and Sentry's webhook config has no way to set arbitrary headers on
// the outbound request. So Sentry calls us (signed), we verify and triage, and we call GitHub
// with the token.
public class GitHubDispatcher
{
    private readonly HttpClient _http;
    private readonly string _repo;   // "owner/name"
    private readonly string _token;

    public GitHubDispatcher(string repo, string token, HttpClient? http = null)
    {
        _repo = repo;
        _token = token;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public virtual async Task<GitHubDispatchResult> DispatchCrashFixAsync(string issueId, string title, string projectSlug, string permalink)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.github.com/repos/{_repo}/dispatches")
        {
            Content = JsonContent.Create(new
            {
                event_type = "sentry-crash",
                // Everything here is untrusted — the title comes from an exception message that
                // may contain attacker-influenced text (a job posting title, an email subject).
                // The workflow treats it as data to look up, never as instructions.
                client_payload = new { issueId, title, projectSlug, permalink },
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("JobFindr-CrashTriage", "1.0"));

        var response = await _http.SendAsync(request);

        // A success here is a 204 with no body, so there is nothing worth reading. On failure,
        // GitHub's error body (e.g. "Resource not accessible by personal access token") is
        // exactly the detail needed to tell a bad token from a wrong repo name from a missing
        // scope — the first real attempt at this returned only a bare "failed" boolean, which
        // wasn't enough to diagnose anything. Bounded so a large unexpected body (this is an
        // external API) can't bloat the log line.
        var body = "";
        if (!response.IsSuccessStatusCode)
        {
            var full = await response.Content.ReadAsStringAsync();
            body = full.Length > 500 ? full[..500] : full;
        }

        return new GitHubDispatchResult(response.IsSuccessStatusCode, (int)response.StatusCode, body);
    }
}

public record GitHubDispatchResult(bool Success, int StatusCode, string ResponseBody);
