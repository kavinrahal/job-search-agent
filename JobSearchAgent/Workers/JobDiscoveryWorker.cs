using System.Text.Json;
using JobSearch.Data;
using JobSearchAgent.Agents;
using JobSearchAgent.Integrations;

namespace JobSearchAgent.Workers;

public class JobDiscoveryWorker
{
    private const int MaxPerRun = 20;
    private const int MaxAgeDays = 14;

    private const int FullFetchThreshold = 400; // chars — below this, attempt a full page fetch

    private readonly AppDbContext _db;
    private readonly IEnumerable<IJobFetcher> _fetchers;
    private readonly JobPostingFetcher _pageFetcher;
    private readonly PostingEvaluator _evaluator;
    private readonly TelegramNotifier? _telegram;

    public JobDiscoveryWorker(
        AppDbContext db,
        IEnumerable<IJobFetcher> fetchers,
        JobPostingFetcher pageFetcher,
        PostingEvaluator evaluator,
        TelegramNotifier? telegram)
    {
        _db = db;
        _fetchers = fetchers;
        _pageFetcher = pageFetcher;
        _evaluator = evaluator;
        _telegram = telegram;
    }

    public async Task<(int Discovered, int Evaluated, int Notified)> RunAsync()
    {
        Console.WriteLine("Job discovery: fetching from all sources...");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var feedItems = new List<JobFeedItem>();
        foreach (var fetcher in _fetchers)
        {
            var items = await fetcher.FetchAllAsync();
            foreach (var item in items.Where(i => seen.Add(i.Url)))
                feedItems.Add(item);
        }

        var cutoff = DateTime.UtcNow.AddDays(-MaxAgeDays);
        var recent = feedItems.Where(i => i.PublishedAt >= cutoff).ToList();
        Console.WriteLine($"Job discovery: {feedItems.Count} total, {recent.Count} within {MaxAgeDays} days.");

        var existingUrls = _db.DiscoveredPostings
            .Where(d => d.Recommendation != "error")
            .Select(d => d.Url)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newItems = recent
            .Where(i => !existingUrls.Contains(i.Url))
            .Take(MaxPerRun)
            .ToList();

        if (newItems.Count == 0)
        {
            Console.WriteLine("Job discovery: nothing new.");
            return (0, 0, 0);
        }

        Console.WriteLine($"Job discovery: {newItems.Count} new postings to evaluate.");

        int evaluated = 0, notified = 0;

        foreach (var item in newItems)
        {
            // Insert immediately so a concurrent/retry run won't re-evaluate the same URL.
            var record = _db.DiscoveredPostings.FirstOrDefault(d => d.Url == item.Url);
            if (record is null)
            {
                record = new DiscoveredPosting { Url = item.Url, Source = item.Source, Title = item.Title, Company = item.Company, DiscoveredAt = DateTime.UtcNow };
                _db.DiscoveredPostings.Add(record);
            }
            else
            {
                record.Recommendation = null;
                record.EvaluatedAt = null;
            }
            _db.SaveChanges();

            try
            {
                Console.WriteLine($"  [{evaluated + 1}/{newItems.Count}] {item.Title}");

                string postingText;
                if (item.Description.Length < FullFetchThreshold)
                {
                    try
                    {
                        postingText = await _pageFetcher.FetchAsync(item.Url);
                        Console.WriteLine($"    (fetched full page — {postingText.Length} chars)");
                    }
                    catch
                    {
                        postingText = FormatPostingText(item);
                        Console.WriteLine($"    (full fetch failed, using feed description)");
                    }
                }
                else
                {
                    postingText = FormatPostingText(item);
                }
                var eval = await _evaluator.EvaluateAsync(postingText, item.Url);

                record.Company = eval.Company;
                record.Title = string.IsNullOrEmpty(eval.RoleTitle) ? item.Title : eval.RoleTitle;
                record.Recommendation = eval.Recommendation;
                record.EvaluationJson = JsonSerializer.Serialize(eval);
                record.DisqualifierHit = eval.DisqualifierHit;
                record.EvaluatedAt = DateTime.UtcNow;
                _db.SaveChanges();

                evaluated++;
                Console.WriteLine($"    => {eval.Recommendation} | {eval.Company}");

                if (_telegram is not null &&
                    (eval.Recommendation is "strong_match" or "good_match"))
                {
                    if (await _telegram.SendAsync(EvalFormatter.Format(eval), "HTML"))
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

            await Task.Delay(1200); // throttle between Claude calls
        }

        return (newItems.Count, evaluated, notified);
    }

    private static string FormatPostingText(JobFeedItem item)
    {
        string salary = item.SalaryMin.HasValue && item.SalaryMax.HasValue
            ? $"${item.SalaryMin:N0} – ${item.SalaryMax:N0} AUD"
            : item.SalaryMin.HasValue
                ? $"From ${item.SalaryMin:N0} AUD"
                : "Not stated";

        return $"""
            Company: {item.Company}
            Job Title: {item.Title}
            Location: {item.Location}
            Salary: {salary}

            {item.Description}
            """;
    }

}
