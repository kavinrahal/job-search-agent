using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace JobSearch.Data;

// IDataProtectionKeyContext: both JobSearch.Api and JobSearchAgent persist their Data
// Protection key ring here (PersistKeysToDbContext) instead of the framework's local-disk
// default, since Railway containers are ephemeral and the API and worker are separate
// processes that both need to decrypt UserSecrets encrypted by either one.
public class AppDbContext : DbContext, IDataProtectionKeyContext
{
    public AppDbContext() { }
    public AppDbContext(DbContextOptions<AppDbContext> optionsBuilder) : base(optionsBuilder) { }

    // The tenant every tenant-scoped query and write in this unit of work is for. Null by
    // default so a caller that forgets to set it gets zero rows back, not every tenant's
    // data — global query filters below compare UserId to this, and `a.UserId == null`
    // matches nothing. Set once per web request (auth middleware) or once per worker run.
    public int? CurrentUserId { get; set; }

    // Explicit opt-in for a context that is deliberately never going to have a CurrentUserId:
    // Data Protection's key-ring resolution (worker only — see JobSearchAgent/Program.cs),
    // the startup owner-seed scope, JobSearchAgent's bootstrap context, and
    // AdminDashboard.Api's keyed read/write contexts. Without this set, accessing one of the
    // seven UserId-filtered DbSet properties below while CurrentUserId is null throws (see
    // GuardedSet) instead of silently returning zero rows — a caller that forgets to set
    // CurrentUserId now fails loudly instead of shipping a silent empty-result bug.
    //
    // Setting this alone does NOT grant visibility into other tenants' rows: the underlying
    // HasQueryFilter predicate is still `UserId == CurrentUserId`, and CurrentUserId is still
    // null, so a filtered query still matches nothing — it just does so quietly instead of
    // throwing. A genuine cross-tenant read (e.g. an admin view across every user)
    // additionally needs `.IgnoreQueryFilters()` on that specific query. This flag exists only
    // to distinguish "no tenant, and that's intentional" from "no tenant, and that's a bug".
    public bool CrossTenantAccess { get; set; }

    // Guards the seven UserId-filtered DbSet properties below (see GuardedSet). Deliberately
    // NOT part of any HasQueryFilter predicate: EF Core's compiled-query cache is shared across
    // every AppDbContext instance backed by the same provider configuration (confirmed via a
    // failing test run — see PR description), so a client-evaluated subexpression inside a
    // query filter that doesn't reference the entity gets folded into a constant ONCE, the
    // first time that query shape compiles, and that frozen result is then reused for every
    // later execution of the same shape regardless of which context instance runs it. Baking
    // "throw" (or "don't throw") into a shared, cached query plan like that would either wedge
    // every future query of that shape after one bad call, or silently stop guarding at all
    // after one good one. Running the check as plain C# at DbSet-property-access time avoids
    // the compiled-query cache entirely — it can't be baked into anything, because it never
    // becomes part of a translated expression tree.
    //
    // This does mean access via a navigation (e.g. `.Include(a => a.Events)` off `Applications`)
    // isn't independently guarded — only the original HasQueryFilter (unchanged, exactly as
    // before this feature) applies there, which still fails closed (zero rows) rather than
    // throwing. In this codebase every such navigation is reached by first going through the
    // owning entity's own guarded DbSet property (see Application.Events usage in
    // JobSearch.Api/Program.cs), so CurrentUserId is already guaranteed non-null (or the access
    // already threw) by the time the navigation is evaluated.
    private DbSet<T> GuardedSet<T>() where T : class
    {
        if (CurrentUserId is null && !CrossTenantAccess)
            throw new InvalidOperationException(
                $"Accessed {typeof(T).Name} (a tenant-scoped table) with CurrentUserId == null " +
                "and CrossTenantAccess not set. Set CurrentUserId to scope this context to a " +
                "tenant, or set CrossTenantAccess = true (and usually .IgnoreQueryFilters() " +
                "too) for a deliberate cross-tenant read.");
        return Set<T>();
    }

    public DbSet<User> Users { get; set; }
    public DbSet<UserProfile> UserProfiles { get; set; }
    public DbSet<UserResume> UserResumes { get; set; }
    public DbSet<UserSecret> UserSecrets { get; set; }
    public DbSet<RawEmailRecord> RawEmails => GuardedSet<RawEmailRecord>();
    public DbSet<ClassificationRecord> Classifications => GuardedSet<ClassificationRecord>();
    public DbSet<Application> Applications => GuardedSet<Application>();
    public DbSet<ApplicationEvent> ApplicationEvents => GuardedSet<ApplicationEvent>();
    public DbSet<SystemHealth> SystemHealth { get; set; }
    public DbSet<DiscoveredPosting> DiscoveredPostings => GuardedSet<DiscoveredPosting>();
    public DbSet<AgentThread> AgentThreads => GuardedSet<AgentThread>();
    public DbSet<ClaudeUsageLog> ClaudeUsageLogs => GuardedSet<ClaudeUsageLog>();
    public DbSet<AnalyticsEvent> AnalyticsEvents { get; set; }
    public DbSet<WorkerLock> WorkerLocks { get; set; }
    public DbSet<SupportMessage> SupportMessages { get; set; }
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
    public DbSet<BetaInvite> BetaInvites { get; set; }
    public DbSet<CrashTriageDispatch> CrashTriageDispatches { get; set; }
    public DbSet<UserVerificationToken> UserVerificationTokens { get; set; }
    public DbSet<SiteStatus> SiteStatuses { get; set; }
    public DbSet<AdminAuditLog> AdminAuditLogs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured) return;
        optionsBuilder.UseNpgsql(GetConnectionString());
    }

    // Priority: DATABASE_URL env var (Railway) → configured value (user-secrets/appsettings) → local default.
    // maxPoolSize caps this process's Npgsql pool explicitly — the Npgsql default (100) is as
    // large as Postgres's own default max_connections, so one process alone could exhaust the
    // database's entire connection budget with nothing left for the other process (API vs.
    // worker each get their own pool) or a manual psql session. Confirmed via `SHOW
    // max_connections;` that this Railway instance's limit is 100 — API (20) + worker (10)
    // leaves 70 connections of headroom for growth, manual access, and margin of error.
    public static string GetConnectionString(string? configured = null, int maxPoolSize = 20)
    {
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        string baseConnectionString;
        if (!string.IsNullOrEmpty(databaseUrl))
            baseConnectionString = ParseDatabaseUrl(databaseUrl);
        else if (!string.IsNullOrEmpty(configured))
            baseConnectionString = configured;
        else
#pragma warning disable S2068 // local dev default only — not a real credential
            baseConnectionString = "Host=localhost;Database=job_search;Username=postgres;Password=postgres";
#pragma warning restore S2068

        return new NpgsqlConnectionStringBuilder(baseConnectionString) { MaxPoolSize = maxPoolSize }.ConnectionString;
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
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.CreditVersion).IsConcurrencyToken();
            // Unique so a SendGrid inbound lookup by token can never match more than one
            // user; Postgres doesn't count NULLs as equal, so the many users without one
            // yet don't collide with each other.
            e.HasIndex(u => u.InboundEmailToken).IsUnique();
        });

        modelBuilder.Entity<BetaInvite>()
            .HasIndex(i => i.Email).IsUnique();

        // No query filter — same reasoning as UserSecret: always looked up by an exact hash
        // (pre-auth, so there's no CurrentUserId to filter by yet) or an exact UserId, never
        // a broad list query.
        modelBuilder.Entity<UserVerificationToken>(e =>
        {
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasIndex(t => t.UserId);
        });

        modelBuilder.Entity<UserProfile>(e =>
        {
            e.HasKey(p => p.UserId);
            e.HasOne(p => p.User)
             .WithOne()
             .HasForeignKey<UserProfile>(p => p.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Same PK-reuse, no-query-filter pattern as UserProfile above, same reasoning — always
        // looked up by an exact known UserId. Row absence is itself meaningful here (see
        // UserResume.cs): it's the "not yet migrated" signal for the resume-builder backfill.
        modelBuilder.Entity<UserResume>(e =>
        {
            e.HasKey(r => r.UserId);
            e.HasOne(r => r.User)
             .WithOne()
             .HasForeignKey<UserResume>(r => r.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // No query filter, same reasoning as UserProfile: UserSecretService always looks
        // this up by an exact, explicitly-passed userId, never a broad list query — a
        // filter here would just risk silently returning null when CurrentUserId doesn't
        // happen to match the userId argument, for no real safety gain over the explicit
        // WHERE clause UserSecretService already applies.
        modelBuilder.Entity<UserSecret>()
            .HasIndex(s => new { s.UserId, s.Key })
            .IsUnique();

        // Gmail message IDs are only unique within a single mailbox, not globally — two
        // different users' inboxes can and do produce colliding MessageIds. The unique
        // constraint has to include UserId or a collision between two users hard-crashes
        // whichever one gets processed second.
        modelBuilder.Entity<RawEmailRecord>(e =>
        {
            e.HasIndex(r => new { r.UserId, r.MessageId }).IsUnique();
            e.HasQueryFilter(r => r.UserId == CurrentUserId);
        });

        modelBuilder.Entity<ClassificationRecord>(e =>
        {
            e.HasIndex(c => new { c.UserId, c.MessageId }).IsUnique();
            e.HasQueryFilter(c => c.UserId == CurrentUserId);
        });

        // Unique at the database level so a concurrent double-delivery of the same Sentry
        // webhook can't produce two agent runs — the second insert loses.
        modelBuilder.Entity<CrashTriageDispatch>(e =>
        {
            e.HasIndex(d => d.SentryIssueId).IsUnique();
            e.HasIndex(d => d.DispatchedAt);
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
        });

        modelBuilder.Entity<ApplicationEvent>(e =>
        {
            e.HasIndex(ev => ev.ApplicationId);
            e.HasIndex(ev => ev.UserId);
            e.HasQueryFilter(ev => ev.UserId == CurrentUserId);
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
            e.HasIndex(t => t.UserId);
            e.HasQueryFilter(t => t.UserId == CurrentUserId);
        });

        modelBuilder.Entity<ClaudeUsageLog>(e =>
        {
            e.HasIndex(l => l.UserId);
            e.HasIndex(l => l.CreatedAt);
            e.HasQueryFilter(l => l.UserId == CurrentUserId);
        });

        modelBuilder.Entity<AnalyticsEvent>(e =>
        {
            e.HasIndex(a => a.EventType);
            e.HasIndex(a => a.CreatedAt);
        });

        modelBuilder.Entity<SupportMessage>()
            .HasIndex(s => s.CreatedAt);

        // No query filter — single-row table read/written cross-tenant by design (see
        // SiteStatus's own doc comment). Always looked up via SingleAsync()/FirstAsync(),
        // never a per-user list.
        modelBuilder.Entity<SiteStatus>();

        // No query filter — same reasoning as AnalyticsEvent/SupportMessage: this table
        // exists for the owner-only Emergency audit trail, a cross-tenant view by design.
        modelBuilder.Entity<AdminAuditLog>(e =>
        {
            e.HasIndex(a => a.PerformedAt);
            e.HasIndex(a => a.TargetUserId);
        });
    }
}
