using System.Text.Json;
using JobSearch.Data;
using Microsoft.EntityFrameworkCore;

namespace JobSearchAgent.Workers;

public class JobDiscoveryWorker
{
    private const int MaxPerRun = 20;
    private const int MaxAgeDays = 14;

    private const int FullFetchThreshold = 400; // chars — below this, attempt a full page fetch

    // Once a posting has failed evaluation this many times (fetch error, Claude error, etc.),
    // treat it as permanently dead rather than retrying forever — a job board that 403s all
    // scraper traffic, or a since-deleted listing, would otherwise burn an HTTP fetch and a
    // Claude call every single run indefinitely with no backoff.
    private const int MaxFailureCount = 3;

    private readonly AppDbContext _db;
    private readonly IEnumerable<IJobFetcher> _fetchers;
    private readonly JobPostingFetcher _pageFetcher;
    private readonly PostingEvaluator _evaluator;
    private readonly SendGridEmailService? _emailer;

    public JobDiscoveryWorker(
        AppDbContext db,
        IEnumerable<IJobFetcher> fetchers,
        JobPostingFetcher pageFetcher,
        PostingEvaluator evaluator,
        SendGridEmailService? emailer = null)
    {
        _db = db;
        _fetchers = fetchers;
        _pageFetcher = pageFetcher;
        _evaluator = evaluator;
        _emailer = emailer;
    }

    public async Task<(int Discovered, int Evaluated, int Notified)> RunAsync()
    {
        var profile = await _db.UserProfiles.FindAsync(_db.CurrentUserId!.Value)
            ?? throw new InvalidOperationException("UserProfile not seeded for the current user.");
        var user = await _db.Users.FindAsync(_db.CurrentUserId!.Value);

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

        // "Seen" = successfully evaluated, OR permanently given up on (hit MaxFailureCount).
        // A still-retryable error record (FailureCount < MaxFailureCount) is deliberately NOT
        // seen, so it gets picked up again next run.
        var existingUrls = await _db.DiscoveredPostings
            .Where(d => d.Recommendation != "error" || d.FailureCount >= MaxFailureCount)
            .Select(d => d.Url)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase);

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
            var record = await _db.DiscoveredPostings.FirstOrDefaultAsync(d => d.Url == item.Url);
            if (record is null)
            {
                record = new DiscoveredPosting { UserId = _db.CurrentUserId!.Value, Url = item.Url, Source = item.Source, Title = item.Title, Company = item.Company, DiscoveredAt = DateTime.UtcNow };
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
                        postingText = item.ToPostingText();
                        Console.WriteLine($"    (full fetch failed, using feed description)");
                    }
                }
                else
                {
                    postingText = item.ToPostingText();
                }
                var eval = await _evaluator.EvaluateAsync(profile, postingText, item.Url);

                record.Company = eval.Company;
                record.Title = string.IsNullOrEmpty(eval.RoleTitle) ? item.Title : eval.RoleTitle;
                // Kept so one-tap CV/cover-letter generation doesn't have to re-fetch a page
                // that may well be unfetchable by then — see DiscoveredPosting.PostingText.
                record.PostingText = postingText;
                record.Recommendation = eval.Recommendation;
                record.EvaluationJson = JsonSerializer.Serialize(eval);
                record.DisqualifierHit = eval.DisqualifierHit;
                record.EvaluatedAt = DateTime.UtcNow;
                record.FailureCount = 0;
                await _db.SaveChangesAsync();

                evaluated++;
                Console.WriteLine($"    => {eval.Recommendation} | {eval.Company}");

                bool isMatch = eval.Recommendation is "strong_match" or "good_match";

                if (_emailer is not null && user is not null && isMatch && !record.EmailNotificationSent)
                {
                    var (subject, body) = EvalFormatter.FormatPlainTextEmail(eval);
                    await _emailer.SendAsync(user.Email, subject, body);
                    record.EmailNotificationSent = true;
                    await _db.SaveChangesAsync();
                    notified++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ! Error: {ex.Message}");
                record.Recommendation = "error";
                record.EvaluatedAt = DateTime.UtcNow;
                record.FailureCount++;
                if (record.FailureCount >= MaxFailureCount)
                    Console.WriteLine($"    ! Giving up permanently after {record.FailureCount} failures — will not retry.");
                await _db.SaveChangesAsync();
            }

            await Task.Delay(1200); // throttle between Claude calls
        }

        return (newItems.Count, evaluated, notified);
    }
}
