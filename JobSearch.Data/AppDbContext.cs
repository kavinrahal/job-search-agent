using Microsoft.EntityFrameworkCore;

namespace JobSearch.Data;

public class AppDbContext : DbContext
{
    public AppDbContext() { }
    public AppDbContext(DbContextOptions<AppDbContext> optionsBuilder) : base(optionsBuilder) { }

    public DbSet<RawEmailRecord> RawEmails { get; set; }
    public DbSet<ClassificationRecord> Classifications { get; set; }
    public DbSet<Application> Applications { get; set; }
    public DbSet<ApplicationEvent> ApplicationEvents { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<SystemHealth> SystemHealth { get; set; }
    public DbSet<DiscoveredPosting> DiscoveredPostings { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured) return;
        optionsBuilder.UseNpgsql(GetConnectionString());
    }

    // Priority: DATABASE_URL env var (Railway) → configured value (user-secrets/appsettings) → local default
    public static string GetConnectionString(string? configured = null)
    {
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrEmpty(databaseUrl))
            return ParseDatabaseUrl(databaseUrl);

        if (!string.IsNullOrEmpty(configured))
            return configured;

#pragma warning disable S2068 // local dev default only — not a real credential
        return "Host=localhost;Database=job_search;Username=postgres;Password=postgres";
#pragma warning restore S2068
    }

    // Convert postgresql://user:pass@host:port/db to Npgsql connection string
    private static string ParseDatabaseUrl(string url)
    {
        var uri = new Uri(url);
        var parts = uri.UserInfo.Split(':');
        return $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={parts[0]};Password={Uri.UnescapeDataString(parts[1])};SSL Mode=Require;Trust Server Certificate=true";
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RawEmailRecord>()
            .HasIndex(e => e.MessageId)
            .IsUnique();

        modelBuilder.Entity<ClassificationRecord>()
            .HasIndex(c => c.MessageId)
            .IsUnique();

        modelBuilder.Entity<Application>(e =>
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

        modelBuilder.Entity<ApplicationEvent>()
            .HasIndex(e => e.ApplicationId);

        modelBuilder.Entity<Notification>(e =>
        {
            e.HasIndex(n => n.SentAt);           // fast query for pending notifications (Telegram)
            e.HasIndex(n => n.WhatsAppSentAt);    // fast query for pending notifications (WhatsApp)
            e.HasIndex(n => n.WhatsAppMessageId); // reply-threading lookup
        });

        modelBuilder.Entity<SystemHealth>()
            .HasIndex(h => h.CheckedAt);

        modelBuilder.Entity<DiscoveredPosting>(e =>
        {
            e.HasIndex(d => d.Url).IsUnique();
            e.HasIndex(d => d.DiscoveredAt);
            e.HasIndex(d => d.Recommendation);
            e.HasIndex(d => d.WhatsAppMessageId); // reply-threading lookup
        });
    }
}
