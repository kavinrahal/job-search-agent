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

    private static readonly Regex JoraPattern = new(
        @"https?://au\.jora\.com/job/([A-Za-z0-9_-]+)",
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

    internal static Dictionary<string, string> ExtractJobUrls(IEnumerable<RawEmail> emails)
    {
        var urls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var email in emails)
        {
            foreach (Match m in SeekPattern.Matches(email.BodyText))
                urls.TryAdd($"https://au.seek.com/job/{m.Groups[1].Value}", "seek_alert");
            foreach (Match m in LinkedInPattern.Matches(email.BodyText))
                urls.TryAdd($"https://www.linkedin.com/jobs/view/{m.Groups[1].Value}", "linkedin_alert");
            foreach (Match m in JoraPattern.Matches(email.BodyText))
                urls.TryAdd($"https://au.jora.com/job/{m.Groups[1].Value}", "jora_alert");
        }
        return urls;
    }

    public async Task<(int Found, int Evaluated, int Notified)> ProcessAsync(
        IEnumerable<RawEmail> alertEmails)
    {
        var urls = ExtractJobUrls(alertEmails);

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
                    if (await _telegram.SendAsync(EvalFormatter.Format(eval, "via job alert"), "HTML"))
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

}
