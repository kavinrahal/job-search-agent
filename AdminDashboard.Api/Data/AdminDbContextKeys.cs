namespace AdminDashboard.Api.Data;

// Keys for the two keyed-DI AppDbContext registrations (see Program.cs) — one backed by
// ReadDatabaseUrl (every normal page/view), one by WriteDatabaseUrl (Emergency actions only).
// Both are plain AppDbContext instances, just pointed at different connection strings; a
// constant pair here beats bare "read"/"write" string literals scattered across every page
// model, where a typo would silently fall back to whatever the default keyed registration is
// (there isn't one, so it would instead throw at request time — better to not have the typo).
public static class AdminDbContextKeys
{
    public const string Read = "read";
    public const string Write = "write";
}
