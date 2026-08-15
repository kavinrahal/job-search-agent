namespace JobSearch.Data;

// Discovery source catalog for the Tier 2 "choose your sources" step. Automatic sources are
// fetched directly, no user setup. Alert sources need a job-alert email from that platform
// forwarded in (Gmail filter + SendGrid pipe — separate tickets). Jooble and Indeed are listed
// for selection but have no fetcher/parser wired up yet; selecting them records intent without
// changing behavior until those are built.
public static class JobSource
{
    public const string Adzuna        = "adzuna";
    public const string Jooble        = "jooble";
    public const string Greenhouse    = "greenhouse";
    public const string Lever         = "lever";
    public const string SeekAlert     = "seek_alert";
    public const string LinkedinAlert = "linkedin_alert";
    public const string IndeedAlert   = "indeed_alert";
    public const string JoraAlert     = "jora_alert";

    public static readonly IReadOnlyList<(string Key, string Label, bool Automatic)> Catalog =
    [
        (Adzuna,        "Adzuna",     true),
        (Jooble,        "Jooble",     true),
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
