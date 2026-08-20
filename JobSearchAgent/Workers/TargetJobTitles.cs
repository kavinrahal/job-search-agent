using System.Text.RegularExpressions;

namespace JobSearchAgent.Workers;

// Extracts the user-provided target_job_titles field from their raw JobCriteria YAML text.
// Deterministic, not AI-derived — the whole point of this field is that the user states
// exactly what to search for instead of having it inferred from looser criteria (see
// JobCriteriaEditor.tsx, the only writer of this key, for the field itself). A plain regex
// against a known single-line scalar, not a full YAML parser — nothing else here needs one.
public static class TargetJobTitles
{
    private static readonly Regex Pattern = new(@"^target_job_titles:\s*(.*)$", RegexOptions.Multiline);

    public static string[] Parse(string? jobCriteria)
    {
        if (string.IsNullOrEmpty(jobCriteria)) return [];

        var match = Pattern.Match(jobCriteria);
        if (!match.Success) return [];

        var raw = match.Groups[1].Value.Trim();
        if (raw.Length >= 2 && ((raw[0] == '"' && raw[^1] == '"') || (raw[0] == '\'' && raw[^1] == '\'')))
            raw = raw[1..^1];

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
