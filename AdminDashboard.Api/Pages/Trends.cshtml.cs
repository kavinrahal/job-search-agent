using AdminDashboard.Api.Data;
using JobSearch.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AdminDashboard.Api.Pages;

public record ChartDay(string Label, int Value);
public record StackedChartDay(string Label, int Cv, int Letter, int Answer);
file sealed record EventRow(int UserId, string EventType, DateTime Date);

// Four hand-rolled bar charts, all derived from AnalyticsEvent rows the backend already
// writes — no new tracking needed. Bucketed by day, last 21 days, via the read connection.
public class TrendsModel : PageModel
{
    private const int WindowDays = 21;

    private readonly AppDbContext _db;

    public TrendsModel([FromKeyedServices(AdminDbContextKeys.Read)] AppDbContext db)
    {
        _db = db;
    }

    public List<ChartDay> Signups { get; private set; } = [];
    public List<ChartDay> Tier2Upgrades { get; private set; } = [];
    public List<StackedChartDay> CreditConsumption { get; private set; } = [];
    public List<ChartDay> DailyActiveUsers { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var today = DateTime.UtcNow.Date;
        var since = today.AddDays(-(WindowDays - 1));
        var days = Enumerable.Range(0, WindowDays).Select(i => since.AddDays(i)).ToList();

        // One query for everything in the window — 21 days of AnalyticsEvents for one app is a
        // small enough row count that grouping in memory per chart is simpler and clearer than
        // four separate provider-translated GROUP BY queries.
        var events = await _db.AnalyticsEvents
            .Where(a => a.CreatedAt >= since)
            .Select(a => new EventRow(a.UserId, a.EventType, a.CreatedAt.Date))
            .ToListAsync();

        // Local function rather than a private static method — EventRow is `file`-scoped
        // (only meaningful within this file), and a file-scoped type can't appear in the
        // signature of a member of a non-file-scoped type like TrendsModel.
        List<ChartDay> BucketCount(IEnumerable<EventRow> matching)
        {
            var byDay = matching.GroupBy(e => e.Date).ToDictionary(g => g.Key, g => g.Count());
            return days.Select(d => new ChartDay(d.ToString("MM/dd"), byDay.GetValueOrDefault(d))).ToList();
        }

        Signups = BucketCount(events.Where(e => e.EventType == AnalyticsEventType.Signup));
        Tier2Upgrades = BucketCount(events.Where(e => e.EventType == AnalyticsEventType.Tier2Upgrade));

        var cvByDay = events.Where(e => e.EventType == AnalyticsEventType.CvGenerated)
            .GroupBy(e => e.Date).ToDictionary(g => g.Key, g => g.Count());
        var letterByDay = events.Where(e => e.EventType == AnalyticsEventType.LetterGenerated)
            .GroupBy(e => e.Date).ToDictionary(g => g.Key, g => g.Count());
        var answerByDay = events.Where(e => e.EventType == AnalyticsEventType.AnswerGenerated)
            .GroupBy(e => e.Date).ToDictionary(g => g.Key, g => g.Count());
        CreditConsumption = days.Select(d => new StackedChartDay(
            d.ToString("MM/dd"),
            cvByDay.GetValueOrDefault(d),
            letterByDay.GetValueOrDefault(d),
            answerByDay.GetValueOrDefault(d))).ToList();

        var dauByDay = events.Where(e => e.EventType == AnalyticsEventType.Login)
            .GroupBy(e => e.Date)
            .ToDictionary(g => g.Key, g => g.Select(e => e.UserId).Distinct().Count());
        DailyActiveUsers = days.Select(d => new ChartDay(d.ToString("MM/dd"), dauByDay.GetValueOrDefault(d))).ToList();
    }
}
