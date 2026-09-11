namespace JobSearch.Data;

// Discovery source catalog for the Tier 2 "choose your sources" step. Automatic sources are
// fetched directly, no user setup. Alert sources need a job-alert email from that platform
// forwarded in (Gmail filter + SendGrid pipe — separate tickets). Indeed is listed for
// selection but has no fetcher/parser wired up yet; selecting it records intent without
// changing behavior until one is built.
//
// Jooble was removed (never had a fetcher/parser — selecting it was a permanent no-op) rather
// than implemented, since nothing else in the codebase depended on it (checked: no fetcher, no
// frontend reference — the source picker renders straight from this Catalog via GET
// /api/v1/sources, nothing hardcodes source names). A user with a stale "jooble" entry already
// saved in EnabledSources isn't affected: Sanitize below filters against ValidKeys (rebuilt from
// this Catalog), so an unrecognized key like a leftover "jooble" is silently dropped, not
// rejected or thrown on — same as any other unknown key already was.
public static class JobSource
{
    public const string Adzuna        = "adzuna";
    public const string Greenhouse    = "greenhouse";
    public const string Lever         = "lever";
    public const string SeekAlert     = "seek_alert";
    public const string LinkedinAlert = "linkedin_alert";
    public const string IndeedAlert   = "indeed_alert";
    public const string JoraAlert     = "jora_alert";

    public static readonly IReadOnlyList<(string Key, string Label, bool Automatic)> Catalog =
    [
        (Adzuna,        "Adzuna",     true),
        (Greenhouse,    "Greenhouse", true),
        (Lever,         "Lever",      true),
        (SeekAlert,     "Seek",       false),
        (LinkedinAlert, "LinkedIn",   false),
        (IndeedAlert,   "Indeed",     false),
        (JoraAlert,     "Jora",       false),
    ];

    private static readonly HashSet<string> ValidKeys = [.. Catalog.Select(c => c.Key)];

    public static List<string> Sanitize(IEnumerable<string> keys) =>
        [.. keys.Where(ValidKeys.Contains).Distinct()];
}
