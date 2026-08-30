using AdminDashboard.Api.Data;
using JobSearch.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AdminDashboard.Api.Pages;

// Overview — stat row + recent signups, entirely via the read connection. No mutations happen
// on this page, so there's no reason for it to touch the write-configured context at all.
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;

    public IndexModel([FromKeyedServices(AdminDbContextKeys.Read)] AppDbContext db)
    {
        _db = db;
    }

    public int TotalUsers { get; private set; }
    public int Tier2Count { get; private set; }
    public int CreditSpend7d { get; private set; }
    public int OpenSupportCount { get; private set; }
    public List<User> RecentSignups { get; private set; } = [];

    public async Task OnGetAsync()
    {
        TotalUsers = await _db.Users.CountAsync();
        Tier2Count = await _db.Users.CountAsync(u => u.Tier == UserTier.Tier2);

        // "Credit spend" has no dedicated ledger — every generation call spends exactly 1
        // credit before it runs (see CreditService.SpendCreditAsync), so a count of the three
        // generation AnalyticsEvent types over the window is the same number.
        var since = DateTime.UtcNow.AddDays(-7);
        string[] spendEventTypes =
        [
            AnalyticsEventType.CvGenerated,
            AnalyticsEventType.LetterGenerated,
            AnalyticsEventType.AnswerGenerated,
        ];
        CreditSpend7d = await _db.AnalyticsEvents
            .CountAsync(a => a.CreatedAt >= since && spendEventTypes.Contains(a.EventType));

        // SupportMessage has no resolved/closed flag (see its own doc comment) — every row is
        // "open" in the only sense this schema currently tracks.
        OpenSupportCount = await _db.SupportMessages.CountAsync();

        RecentSignups = await _db.Users
            .OrderByDescending(u => u.CreatedAt)
            .Take(10)
            .ToListAsync();
    }
}
