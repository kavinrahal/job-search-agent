using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminDashboard.Api.Pages;

public record RevenueBySource(string Source, decimal Amount);
public record TopSpender(string Email, decimal Amount);

// Gated behind Features:RevenueEnabled (default false) — this flips on once Stripe
// integration ships. Everything on this page is realistic placeholder data, not a DB query:
// there is no real revenue to query yet. Swapping in real data later needs no shape change to
// this page, just replacing the constants below with real queries once the flag is on.
public class RevenueModel : PageModel
{
    private readonly IConfiguration _config;

    public RevenueModel(IConfiguration config)
    {
        _config = config;
    }

    public decimal Mrr { get; private set; }
    public decimal Revenue30d { get; private set; }
    public int PayingUsers { get; private set; }
    public decimal AvgRevenuePerUser { get; private set; }
    public List<ChartDay> RevenueTrend { get; private set; } = [];
    public List<RevenueBySource> BySource { get; private set; } = [];
    public List<TopSpender> TopSpenders { get; private set; } = [];

    public IActionResult OnGet()
    {
        // Nav item is hidden when the flag is off (see _Layout.cshtml) — the route itself
        // also has to 404, not just look unreachable, so navigating here directly while
        // disabled behaves the same as a route that doesn't exist.
        if (!_config.GetValue<bool>("Features:RevenueEnabled"))
            return NotFound();

        Mrr = 2_340m;
        Revenue30d = 2_180m;
        PayingUsers = 47;
        AvgRevenuePerUser = Math.Round(Mrr / Math.Max(1, PayingUsers), 2);

        // 21-day placeholder trend, gentle upward drift — matches Trends' own window so the
        // two pages read consistently.
        var today = DateTime.UtcNow.Date;
        var baseline = new[] { 58, 62, 55, 70, 64, 30, 28, 66, 71, 68, 75, 40, 35, 80, 84, 79, 90, 45, 42, 95, 101 };
        RevenueTrend = baseline
            .Select((v, i) => new ChartDay(today.AddDays(-(baseline.Length - 1 - i)).ToString("MM/dd"), v))
            .ToList();

        BySource =
        [
            new RevenueBySource("Tier 2 subscriptions", 1_960m),
            new RevenueBySource("One-off credit top-ups", 220m),
        ];

        TopSpenders =
        [
            new TopSpender("placeholder-1@example.com", 189m),
            new TopSpender("placeholder-2@example.com", 156m),
            new TopSpender("placeholder-3@example.com", 142m),
            new TopSpender("placeholder-4@example.com", 118m),
            new TopSpender("placeholder-5@example.com", 99m),
        ];

        return Page();
    }
}
