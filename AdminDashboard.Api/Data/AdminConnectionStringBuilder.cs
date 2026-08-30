using Npgsql;

namespace AdminDashboard.Api.Data;

// Resolves AdminDashboard.Api's own two connection strings (ReadDatabaseUrl / WriteDatabaseUrl)
// independently of JobSearch.Data's AppDbContext.GetConnectionString, which is intentionally
// tied to the main app's shared DATABASE_URL Railway convention — this project needs two
// distinct, separately configured values instead of one, so it can't reuse that helper as-is.
//
// WriteDatabaseUrl defaults to the same value as ReadDatabaseUrl when not separately configured
// (see the class's own Resolve() overload below) — provisioning a real least-privilege
// read-only Postgres role is a later infra step; the code is already structured so that swap-in
// requires zero changes beyond setting the env var.
public static class AdminConnectionStringBuilder
{
    // Accepts either a postgres://user:pass@host:port/db URL (Railway's usual form) or an
    // already-Npgsql-formatted "Host=...;Database=...;..." string, same two shapes
    // AppDbContext.GetConnectionString accepts.
    public static string Build(string raw, int maxPoolSize)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("Connection string must not be empty.", nameof(raw));

        var baseConnectionString =
            raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
                ? ParseDatabaseUrl(raw)
                : raw;

        return new NpgsqlConnectionStringBuilder(baseConnectionString) { MaxPoolSize = maxPoolSize }.ConnectionString;
    }

    // Resolves the write connection string: the configured WriteDatabaseUrl if set, otherwise
    // the already-resolved read connection string. Kept as its own testable method rather than
    // inlined in Program.cs so the default-to-read behavior has a direct unit test.
    public static string ResolveWrite(string? configuredWriteUrl, string resolvedReadConnectionString, int maxPoolSize)
    {
        return string.IsNullOrWhiteSpace(configuredWriteUrl)
            ? resolvedReadConnectionString
            : Build(configuredWriteUrl, maxPoolSize);
    }

    private static string ParseDatabaseUrl(string url)
    {
        var uri = new Uri(url);
        var parts = uri.UserInfo.Split(':');
        return $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};Username={parts[0]};Password={Uri.UnescapeDataString(parts[1])};SSL Mode=Require;Trust Server Certificate=true";
    }
}
