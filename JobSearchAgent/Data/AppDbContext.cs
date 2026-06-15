using Microsoft.EntityFrameworkCore;

namespace JobSearchAgent.Data;

public class AppDbContext : DbContext
{
    public DbSet<RawEmailRecord> RawEmails { get; set; }
    public DbSet<ClassificationRecord> Classifications { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        string path = ResolveDbPath();
        Console.WriteLine($"[DB] {path}");
        options.UseSqlite($"Data Source={path}");
    }

    internal static string ResolveDbPath()
    {
        const string dbFile = "job_search.db";

        // Find repo root by walking up from the exe until we find .gitignore
        string? repoRoot = null;
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, ".gitignore")))
            {
                repoRoot = dir.FullName;
                break;
            }
            dir = dir.Parent;
        }

        if (repoRoot is not null)
        {
            string atRoot  = Path.Combine(repoRoot, dbFile);
            string atAgent = Path.Combine(repoRoot, "JobSearchAgent", dbFile);
            if (File.Exists(atRoot))  return atRoot;
            if (File.Exists(atAgent)) return atAgent;
            return atRoot; // new install: place at repo root
        }

        // Fallback: CWD
        return Path.Combine(Directory.GetCurrentDirectory(), dbFile);
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
