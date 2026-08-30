using AdminDashboard.Api.Data;
using JobSearch.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AdminDashboard.Api.Pages;

// Searchable/filterable user list — email, tier, credit balance, verified status, joined date.
// Read-only, so entirely via the read connection.
public class UsersModel : PageModel
{
    private readonly AppDbContext _db;

    public UsersModel([FromKeyedServices(AdminDbContextKeys.Read)] AppDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Tier { get; set; }

    public List<User> Users { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var query = _db.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var needle = Search.Trim();
            query = query.Where(u => EF.Functions.ILike(u.Email, $"%{needle}%"));
        }

        if (Tier is UserTier.Tier1 or UserTier.Tier2)
        {
            query = query.Where(u => u.Tier == Tier);
        }

        Users = await query.OrderByDescending(u => u.CreatedAt).Take(200).ToListAsync();
    }
}
