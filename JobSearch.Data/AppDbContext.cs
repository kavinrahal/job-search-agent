using Microsoft.EntityFrameworkCore;

namespace JobSearch.Data;

public class AppDbContext : DbContext
{
    public AppDbContext() { }
    public AppDbContext(DbContextOptions<AppDbContext> optionsBuilder) : base(optionsBuilder) { }

    // The tenant every tenant-scoped query and write in this unit of work is for. Null by
    // default so a caller that forgets to set it gets zero rows back, not every tenant's
    // data — global query filters below compare UserId to this, and `a.UserId == null`
    // matches nothing. Set once per web request (auth middleware) or once per worker run.
    public int? CurrentUserId { get; set; }

    public DbSet<User> Users { get; set; }
    public DbSet<UserProfile> UserProfiles { get; set; }
    public DbSet<RawEmailRecord> RawEmails { get; set; }
    public DbSet<ClassificationRecord> Classifications { get; set; }
    public DbSet<Application> Applications { get; set; }
    public DbSet<ApplicationEvent> ApplicationEvents { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<SystemHealth> SystemHealth { get; set; }
    public DbSet<DiscoveredPosting> DiscoveredPostings { get; set; }
    public DbSet<AgentThread> AgentThreads { get; set; }

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
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<UserProfile>(e =>
        {
            e.HasKey(p => p.UserId);
            e.HasOne(p => p.User)
             .WithOne()
             .HasForeignKey<UserProfile>(p => p.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RawEmailRecord>(e =>
        {
            e.HasIndex(r => r.MessageId).IsUnique();
            e.HasIndex(r => r.UserId);
            e.HasQueryFilter(r => r.UserId == CurrentUserId);
        });

        modelBuilder.Entity<ClassificationRecord>(e =>
        {
            e.HasIndex(c => c.MessageId).IsUnique();
            e.HasIndex(c => c.UserId);
            e.HasQueryFilter(c => c.UserId == CurrentUserId);
        });

        modelBuilder.Entity<Application>(e =>
        {
            e.HasIndex(a => new { a.Company, a.RoleTitle });
            e.HasIndex(a => a.UserId);
            e.HasQueryFilter(a => a.UserId == CurrentUserId);
            e.HasMany(a => a.Events)
             .WithOne(ev => ev.Application)
             .HasForeignKey(ev => ev.ApplicationId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(a => a.Notifications)
             .WithOne(n => n.Application)
             .HasForeignKey(n => n.ApplicationId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ApplicationEvent>(e =>
        {
            e.HasIndex(ev => ev.ApplicationId);
            e.HasIndex(ev => ev.UserId);
            e.HasQueryFilter(ev => ev.UserId == CurrentUserId);
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.HasIndex(n => n.SentAt);           // fast query for pending notifications (Telegram)
            e.HasIndex(n => n.UserId);
            e.HasQueryFilter(n => n.UserId == CurrentUserId);
        });

        modelBuilder.Entity<SystemHealth>()
            .HasIndex(h => h.CheckedAt);

        modelBuilder.Entity<DiscoveredPosting>(e =>
        {
            // Composite, not a bare Url unique index: two different users matching the same
            // posting URL must each get their own row — a global Url uniqueness constraint
            // would throw on the second user's insert even though the per-user LINQ dedup
            // (query-filtered by UserId) correctly treats it as new for them.
            e.HasIndex(d => new { d.UserId, d.Url }).IsUnique();
            e.HasIndex(d => d.DiscoveredAt);
            e.HasIndex(d => d.Recommendation);
            e.HasQueryFilter(d => d.UserId == CurrentUserId);
        });

        modelBuilder.Entity<AgentThread>(e =>
        {
            e.HasIndex(t => t.LastMessageId).IsUnique(); // reply-threading lookup
            e.HasIndex(t => t.UserId);
            e.HasQueryFilter(t => t.UserId == CurrentUserId);
        });
    }
}
