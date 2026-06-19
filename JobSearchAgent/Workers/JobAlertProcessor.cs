using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JobSearch.Data;
using JobSearchAgent.Agents;
using JobSearchAgent.Integrations;
using JobSearchAgent.Models;

namespace JobSearchAgent.Workers;

public class JobAlertProcessor
{
    private const int MaxPerRun = 30;

    private static readonly Regex SeekPattern = new(
        @"https?://(?:www\.seek\.com\.au|au\.seek\.com)/job/(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LinkedInPattern = new(
        @"https?://(?:www\.)?linkedin\.com/jobs/view/(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly JobPostingFetcher _fetcher;
    private readonly PostingEvaluator _evaluator;
    private readonly TelegramNotifier? _telegram;

    public JobAlertProcessor(
        AppDbContext db,
        JobPostingFetcher fetcher,
        PostingEvaluator evaluator,
        TelegramNotifier? telegram)
    {
        _db = db;
        _fetcher = fetcher;
        _evaluator = evaluator;
        _telegram = telegram;
    }

    public async Task<(int Found, int Evaluated, int Notified)> ProcessAsync(
        IEnumerable<RawEmail> alertEmails)
    {
        // Extract and normalise job URLs from all alert emails
        var urls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var email in alertEmails)
        {
            foreach (Match m in SeekPattern.Matches(email.BodyText))
            {
                var canonical = $"https://au.seek.com/job/{m.Groups[1].Value}";
                urls.TryAdd(canonical, "seek_alert");
            }
            foreach (Match m in LinkedInPattern.Matches(email.BodyText))
            {
                var canonical = $"https://www.linkedin.com/jobs/view/{m.Groups[1].Value}";
                urls.TryAdd(canonical, "linkedin_alert");
            }
        }

        if (urls.Count == 0)
        {
            Console.WriteLine("Job alerts: no job URLs found in alert emails.");
            return (0, 0, 0);
        }

        // Deduplicate against already-stored postings
        var existingUrls = _db.DiscoveredPostings
            .Select(d => d.Url)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newUrls = urls
            .Where(kv => !existingUrls.Contains(kv.Key))
            .Take(MaxPerRun)
            .ToList();

        Console.WriteLine($"Job alerts: {urls.Count} URLs found, {newUrls.Count} new.");

        if (newUrls.Count == 0) return (urls.Count, 0, 0);

        int evaluated = 0, notified = 0;

        foreach (var (url, source) in newUrls)
        {
            var record = new DiscoveredPosting
            {
                Url = url,
                Source = source,
                Title = "",
                DiscoveredAt = DateTime.UtcNow,
            };
            _db.DiscoveredPostings.Add(record);
            _db.SaveChanges();

            try
            {
                Console.WriteLine($"  [{evaluated + 1}/{newUrls.Count}] {url}");

                var postingText = await _fetcher.FetchAsync(url);
                var eval = await _evaluator.EvaluateAsync(postingText, url);

                record.Company = eval.Company;
                record.Title = eval.RoleTitle;
                record.Recommendation = eval.Recommendation;
                record.EvaluationJson = JsonSerializer.Serialize(eval);
                record.DisqualifierHit = eval.DisqualifierHit;
                record.EvaluatedAt = DateTime.UtcNow;
                _db.SaveChanges();

                evaluated++;
                Console.WriteLine($"    => {eval.Recommendation} | {eval.Company} — {eval.RoleTitle}");

                if (_telegram is not null &&
                    eval.Recommendation is "strong_match" or "good_match")
                {
                    if (await _telegram.SendAsync(FormatNotification(eval), "HTML"))
                    {
                        record.NotificationSent = true;
                        _db.SaveChanges();
                        notified++;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ! Error: {ex.Message}");
                record.Recommendation = "error";
                record.EvaluatedAt = DateTime.UtcNow;
                _db.SaveChanges();
            }

            await Task.Delay(1200);
        }

        return (urls.Count, evaluated, notified);
    }

    private static string FormatNotification(PostingEvaluation ev)
    {
        var rec = ev.Recommendation switch
        {
            "strong_match" => "STRONG MATCH",
            "good_match"   => "GOOD MATCH",
            _              => ev.Recommendation?.ToUpperInvariant() ?? "",
        };

        var sb = new StringBuilder();
        sb.AppendLine($"<b>{ev.Company} — {ev.RoleTitle}</b>");
        sb.AppendLine($"<b>{rec}</b> (via job alert)");
        sb.AppendLine();
        sb.AppendLine($"Location: {ev.LocationDetail} ({ev.LocationMatch})");
        sb.AppendLine($"Experience: {ev.ExperienceDetail} ({ev.ExperienceMatch})");

        var backend = ev.BackendTechnologies.Length > 0
            ? string.Join(", ", ev.BackendTechnologies) : "not stated";
        sb.AppendLine($"Backend: {backend} ({ev.BackendMatch})");

        sb.AppendLine($"Salary: {ev.SalaryDetail ?? "not stated"} ({ev.SalaryAssessment})");

        if (ev.OrangeFlags.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("<b>Orange flags:</b>");
            foreach (var flag in ev.OrangeFlags)
                sb.AppendLine($"• {flag}");
        }

        sb.AppendLine();
        sb.AppendLine($"<b>Rationale:</b> {ev.Rationale}");

        if (ev.SourceUrl is not null)
            sb.Append($"\n<a href=\"{ev.SourceUrl}\">View posting</a>");

        return sb.ToString().TrimEnd();
    }
}
