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

    public virtual async Task<bool> DispatchCrashFixAsync(string issueId, string title, string projectSlug, string permalink)
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
        return response.IsSuccessStatusCode;
    }
}
