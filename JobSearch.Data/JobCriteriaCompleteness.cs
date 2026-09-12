using YamlDotNet.Serialization;

namespace JobSearch.Data;

// Server-side mirror of JobSearch.Web/src/lib/criteriaCompleteness.ts's isCriteriaComplete /
// getMissingCriteriaFields — the single source of truth the frontend uses (onboarding wizard,
// full editor, dashboard nudge banner) for "is criteria actually filled in enough to be
// useful". The discovery worker needs the same notion server-side, before it ever runs
// discovery/evaluation for a user (see JobSearchAgent/Program.cs's discoveryUsers filter) —
// evaluating postings against sparse criteria produces evaluations that look "unrelated to
// skills/industry" simply because most of the user's real criteria didn't exist yet when
// evaluated.
//
// Deliberately a presence-only re-implementation, not a full port of jobCriteriaYaml.ts's rich
// parsing/migration/back-compat logic (multiple legacy shapes per field, "clean match" gating,
// etc.) — that file's job is lossless round-tripping through the editor's form; this only needs
// "is there a real value here at all", which a permissive YamlDotNet deserialization answers
// directly. Every discovery-eligible user's criteria was written by JobCriteriaEditor.tsx (the
// only writer), which always emits every key below, so the richer legacy-shape handling in
// jobCriteriaYaml.ts mostly exists for hand-edited/pre-migration text this worker path doesn't
// need to render back to a form. Keep this in sync with criteriaCompleteness.ts's field list
// if that ever changes — there is no shared schema between the TS and C# sides today.
//
// Tier is NOT a parameter here (unlike the frontend's isCriteriaComplete(data, tier)): the only
// caller is the Tier 2 aggregator/ATS discovery loop in JobSearchAgent/Program.cs, so
// targetJobTitles is unconditionally required, matching the frontend's tier === "Tier2" branch.
//
// Sponsorship and disqualifiers are excluded for the same reason criteriaCompleteness.ts
// excludes them — both are legitimately-optional, so "blank" is a valid complete answer there.
public static class JobCriteriaCompleteness
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static bool IsComplete(string? jobCriteria)
    {
        if (string.IsNullOrWhiteSpace(jobCriteria)) return false;

        // target_job_titles: reuses TargetJobTitles' own regex-based extraction rather than
        // re-parsing it here, so there's exactly one place that knows how to read this field.
        if (TargetJobTitles.Parse(jobCriteria).Length == 0) return false;

        CriteriaRoot root;
        try
        {
            root = Deserializer.Deserialize<CriteriaRoot>(jobCriteria) ?? new CriteriaRoot();
        }
        catch
        {
            // Same never-throws contract as JobLocation/BackgroundYamlParser: JobCriteria is
            // user-authored/AI-touched YAML, not a strict contract — a shape mismatch means
            // "can't confirm this is complete", so it's treated as incomplete rather than
            // throwing on a hot discovery-worker path.
            return false;
        }

        // experience.candidate_current
        if (string.IsNullOrWhiteSpace(root.Experience.CandidateCurrent)) return false;

        // skills[0] — position is priority; only the first entry needs to be non-blank, same
        // check as the frontend's `data.skills[0]`.
        if (root.Skills.Count == 0 || string.IsNullOrWhiteSpace(root.Skills[0])) return false;

        // employment_type_preference
        if (root.EmploymentTypePreference.Count == 0) return false;

        // location.countries
        if (root.Location.Countries.Count == 0 || root.Location.Countries.All(string.IsNullOrWhiteSpace)) return false;

        // location.remote/hybrid/on_site — at least one work arrangement accepted. Each block
        // defaults to accepted=true when absent from the YAML entirely, mirroring
        // jobCriteriaYaml.ts's DEFAULTS (remoteAccepted/hybridAccepted/onsiteAccepted all
        // default true) — only an explicit `accepted: false` on all three counts as missing.
        if (!root.Location.Remote.Accepted && !root.Location.Hybrid.Accepted && !root.Location.OnSite.Accepted) return false;

        // salary — complete if any of the three form fields the frontend checks
        // (salaryMin/salaryTargetMin/salaryMax) would have a value. Those are populated from
        // minimum_acceptable/target_base/target_max (old/simple shape) or
        // thresholds.acceptable_minimum/thresholds.target_range (current shape) — presence of
        // any one of these is sufficient here.
        bool hasSalary = root.Salary.MinimumAcceptable is not null
            || root.Salary.TargetBase is not null
            || root.Salary.TargetMax is not null
            || root.Salary.Thresholds?.AcceptableMinimum is not null
            || (root.Salary.Thresholds?.TargetRange?.Count ?? 0) > 0;
        if (!hasSalary) return false;

        return true;
    }

    private sealed class CriteriaRoot
    {
        public ExperienceBlock Experience { get; set; } = new();
        public List<string> Skills { get; set; } = [];
        public List<string> EmploymentTypePreference { get; set; } = ["full_time"];
        public LocationBlock Location { get; set; } = new();
        public SalaryBlock Salary { get; set; } = new();
    }

    private sealed class ExperienceBlock
    {
        public string? CandidateCurrent { get; set; }
    }

    private sealed class LocationBlock
    {
        public List<string> Countries { get; set; } = [];
        public AcceptedBlock Remote { get; set; } = new();
        public AcceptedBlock Hybrid { get; set; } = new();
        public AcceptedBlock OnSite { get; set; } = new();
    }

    private sealed class AcceptedBlock
    {
        public bool Accepted { get; set; } = true;
    }

    private sealed class SalaryBlock
    {
        public decimal? MinimumAcceptable { get; set; }
        public decimal? TargetBase { get; set; }
        public decimal? TargetMax { get; set; }
        public ThresholdsBlock? Thresholds { get; set; }
    }

    private sealed class ThresholdsBlock
    {
        public decimal? AcceptableMinimum { get; set; }
        public List<decimal>? TargetRange { get; set; }
    }
}
