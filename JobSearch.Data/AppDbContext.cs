using Microsoft.EntityFrameworkCore;

namespace JobSearch.Data;

public class AppDbContext : DbContext
{
    public AppDbContext() { }
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<RawEmailRecord> RawEmails { get; set; }
    public DbSet<ClassificationRecord> Classifications { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (options.IsConfigured) return;
        options.UseNpgsql(GetConnectionString());
    }

    // Priority: DATABASE_URL env var (Railway) → configured value (user-secrets/appsettings) → local default
    public static string GetConnectionString(string? configured = null)
    {
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrEmpty(databaseUrl))
            return ParseDatabaseUrl(databaseUrl);

        if (!string.IsNullOrEmpty(configured))
            return configured;

        return "Host=localhost;Database=job_search;Username=postgres;Password=postgres";
    }

    // Convert postgresql://user:pass@host:port/db to Npgsql connection string
    private static string ParseDatabaseUrl(string url)
    {
        var uri = new Uri(url);
        var parts = uri.UserInfo.Split(':');
        return $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={parts[0]};Password={Uri.UnescapeDataString(parts[1])};SSL Mode=Require;Trust Server Certificate=true";
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<RawEmailRecord>()
            .HasIndex(e => e.MessageId)
            .IsUnique();

        builder.Entity<ClassificationRecord>()
            .HasIndex(c => c.MessageId)
            .IsUnique();
    }
}
