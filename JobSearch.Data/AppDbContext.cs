using Microsoft.EntityFrameworkCore;

namespace JobSearch.Data;

public class AppDbContext : DbContext
{
    public AppDbContext() { }
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<RawEmailRecord> RawEmails { get; set; }
    public DbSet<ClassificationRecord> Classifications { get; set; }
    public DbSet<Application> Applications { get; set; }
    public DbSet<ApplicationEvent> ApplicationEvents { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<SystemHealth> SystemHealth { get; set; }

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

        builder.Entity<Application>(e =>
        {
            e.HasIndex(a => new { a.Company, a.RoleTitle });
            e.HasMany(a => a.Events)
             .WithOne(ev => ev.Application)
             .HasForeignKey(ev => ev.ApplicationId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(a => a.Notifications)
             .WithOne(n => n.Application)
             .HasForeignKey(n => n.ApplicationId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ApplicationEvent>()
            .HasIndex(e => e.ApplicationId);

        builder.Entity<Notification>()
            .HasIndex(n => n.SentAt);  // fast query for pending notifications

        builder.Entity<SystemHealth>()
            .HasIndex(h => h.CheckedAt);
    }
}
