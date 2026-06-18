using System.Text;
using System.Text.Json;
using JobSearch.Data;
using JobSearchAgent.Agents;
using JobSearchAgent.Integrations;

namespace JobSearchAgent.Workers;

public class JobDiscoveryWorker
{
    private const int MaxPerRun = 20;
    private const int MaxAgeDays = 14;

    private readonly AppDbContext _db;
    private readonly SeekRssFetcher _seek;
    private readonly JobPostingFetcher _fetcher;
    private readonly PostingEvaluator _evaluator;
    private readonly TelegramNotifier? _telegram;

    public JobDiscoveryWorker(
        AppDbContext db,
        SeekRssFetcher seek,
        JobPostingFetcher fetcher,
        PostingEvaluator evaluator,
        TelegramNotifier? telegram)
    {
        _db = db;
        _seek = seek;
        _fetcher = fetcher;
        _evaluator = evaluator;
        _telegram = telegram;
    }

    public async Task<(int Discovered, int Evaluated, int Notified)> RunAsync()
    {
        Console.WriteLine("Job discovery: fetching Seek RSS feeds...");
        var feedItems = await _seek.FetchAllAsync();

        var cutoff = DateTime.UtcNow.AddDays(-MaxAgeDays);
        var recent = feedItems.Where(i => i.PublishedAt >= cutoff).ToList();
        Console.WriteLine($"Job discovery: {feedItems.Count} total, {recent.Count} within {MaxAgeDays} days.");

        var existingUrls = _db.DiscoveredPostings
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
            var record = new DiscoveredPosting
            {
                Url = item.Url,
                Source = "seek",
                Title = item.Title,
                DiscoveredAt = DateTime.UtcNow,
            };
            _db.DiscoveredPostings.Add(record);
            _db.SaveChanges();

            try
            {
                Console.WriteLine($"  [{evaluated + 1}/{newItems.Count}] {item.Title}");

                var postingText = await _fetcher.FetchAsync(item.Url);
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

            await Task.Delay(1200); // throttle between Claude calls
        }

        return (newItems.Count, evaluated, notified);
    }

    private static string FormatNotification(PostingEvaluation ev)
    {
        var rec = ev.Recommendation switch
        {
            "strong_match" => "STRONG MATCH",
            "good_match"   => "GOOD MATCH",
            _              => ev.Recommendation.ToUpperInvariant(),
        };

        var sb = new StringBuilder();
        sb.AppendLine($"<b>{ev.Company} — {ev.RoleTitle}</b>");
        sb.AppendLine($"<b>Recommendation: {rec}</b>");
        sb.AppendLine();
        sb.AppendLine("<b>Dimensions:</b>");
        sb.AppendLine($"Sponsorship: {ev.SponsorshipVerdict}");
        sb.AppendLine($"Location: {ev.LocationDetail} ({ev.LocationMatch})");
        sb.AppendLine($"Experience: {ev.ExperienceDetail} ({ev.ExperienceMatch})");

        var backend = ev.BackendTechnologies.Length > 0
            ? string.Join(", ", ev.BackendTechnologies)
            : "not stated";
        sb.AppendLine($"Backend: {backend} ({ev.BackendMatch})");

        var frontend = ev.FrontendTechnologies.Length > 0
            ? string.Join(", ", ev.FrontendTechnologies)
            : "not stated";
        sb.AppendLine($"Frontend: {frontend} ({ev.FrontendMatch})");

        sb.AppendLine($"Salary: {ev.SalaryDetail ?? "not stated"} ({ev.SalaryAssessment})");
        sb.AppendLine();

        if (ev.OrangeFlags.Length > 0)
        {
            sb.AppendLine("<b>Orange flags:</b>");
            foreach (var flag in ev.OrangeFlags)
                sb.AppendLine($"• {flag}");
        }
        else
        {
            sb.AppendLine("<b>Orange flags:</b> none");
        }

        sb.AppendLine();
        sb.AppendLine($"<b>Rationale:</b> {ev.Rationale}");

        if (ev.SourceUrl is not null)
            sb.Append($"\n<a href=\"{ev.SourceUrl}\">View posting</a>");

        return sb.ToString().TrimEnd();
    }
}
