using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace JobSearch.Data;

// Used only by `dotnet ef migrations` tooling — not at runtime.
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(AppDbContext.GetConnectionString())
            .Options;
        return new AppDbContext(options);
    }
}
