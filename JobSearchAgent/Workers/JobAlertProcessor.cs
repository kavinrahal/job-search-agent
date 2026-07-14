using System.Text.Json;
using System.Text.RegularExpressions;
using JobSearch.Data;
using JobSearchAgent.Agents;
using JobSearchAgent.Integrations;
using JobSearchAgent.Models;
using Microsoft.EntityFrameworkCore;

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
        foreach (var bodyText in emails.Select(e => e.BodyText))
        {
            foreach (Match m in SeekPattern.Matches(bodyText))
                urls.TryAdd($"https://au.seek.com/job/{m.Groups[1].Value}", "seek_alert");
            foreach (Match m in LinkedInPattern.Matches(bodyText))
                urls.TryAdd($"https://www.linkedin.com/jobs/view/{m.Groups[1].Value}", "linkedin_alert");
            foreach (Match m in JoraPattern.Matches(bodyText))
                urls.TryAdd($"https://au.jora.com/job/{m.Groups[1].Value}", "jora_alert");
        }
        return urls;
    }

    public async Task<(int Found, int Evaluated, int Notified)> ProcessAsync(
        IEnumerable<RawEmail> alertEmails)
    {
        var emailList = alertEmails.ToList();
        var urls = ExtractJobUrls(emailList);

        if (urls.Count == 0)
        {
            Console.WriteLine("Job alerts: no job URLs found in alert emails.");
            return (0, 0, 0);
        }

        // Build URL → email body index so we can fall back to alert content
        // when the job page itself is unreachable (e.g. private hostname, geo-block).
        var fallbackContext = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var bodyText in emailList.Select(e => e.BodyText))
        {
            foreach (Match m in SeekPattern.Matches(bodyText))
                fallbackContext.TryAdd($"https://au.seek.com/job/{m.Groups[1].Value}", bodyText);
            foreach (Match m in LinkedInPattern.Matches(bodyText))
                fallbackContext.TryAdd($"https://www.linkedin.com/jobs/view/{m.Groups[1].Value}", bodyText);
            foreach (Match m in JoraPattern.Matches(bodyText))
                fallbackContext.TryAdd($"https://au.jora.com/job/{m.Groups[1].Value}", bodyText);
        }

        // Deduplicate against already-stored postings. Exclude "error" records so
        // transient failures (403, timeout) are retried on the next run.
        var existingUrls = await _db.DiscoveredPostings
            .Where(d => d.Recommendation != "error")
            .Select(d => d.Url)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase);

        var newUrls = urls
            .Where(kv => !existingUrls.Contains(kv.Key))
            .Take(MaxPerRun)
            .ToList();

        Console.WriteLine($"Job alerts: {urls.Count} URLs found, {newUrls.Count} new.");

        if (newUrls.Count == 0) return (urls.Count, 0, 0);

        int evaluated = 0, notified = 0;

        foreach (var (url, source) in newUrls)
        {
            var record = await _db.DiscoveredPostings.FirstOrDefaultAsync(d => d.Url == url);
            if (record is null)
            {
                record = new DiscoveredPosting { Url = url, Source = source, Title = "", DiscoveredAt = DateTime.UtcNow };
                _db.DiscoveredPostings.Add(record);
            }
            else
            {
                record.Recommendation = null;
                record.EvaluatedAt = null;
            }
            await _db.SaveChangesAsync();

            try
            {
                Console.WriteLine($"  [{evaluated + 1}/{newUrls.Count}] {url}");

                string postingText;
                try
                {
                    postingText = await _fetcher.FetchAsync(url);
                }
                catch (Exception fetchEx) when (fallbackContext.ContainsKey(url))
                {
                    Console.WriteLine($"    (fetch failed: {fetchEx.Message} — evaluating from email alert content)");
                    postingText = $"Source URL: {url}\n\n[Job page could not be fetched. Evaluate based on the email alert content below.]\n\n{fallbackContext[url]}";
                }

                var eval = await _evaluator.EvaluateAsync(postingText, url);

                record.Company = eval.Company;
                record.Title = eval.RoleTitle;
                record.Recommendation = eval.Recommendation;
                record.EvaluationJson = JsonSerializer.Serialize(eval);
                record.DisqualifierHit = eval.DisqualifierHit;
                record.EvaluatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                evaluated++;
                Console.WriteLine($"    => {eval.Recommendation} | {eval.Company} — {eval.RoleTitle}");

                if (_telegram is not null &&
                    eval.Recommendation is "strong_match" or "good_match" &&
                    await _telegram.SendAsync(EvalFormatter.Format(eval, "via job alert"), "HTML"))
                {
                    record.NotificationSent = true;
                    await _db.SaveChangesAsync();
                    notified++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ! Error: {ex.Message}");
                record.Recommendation = "error";
                record.EvaluatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            await Task.Delay(1200);
        }

        return (urls.Count, evaluated, notified);
    }


}
