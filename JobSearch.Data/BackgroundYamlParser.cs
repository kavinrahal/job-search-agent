using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace JobSearch.Data;

// The only place skills/context/background.yaml-shaped text gets structurally parsed in this
// codebase. Every other current consumer (CvTailorAgent, CoverLetterAgent, AccuracyVerifierAgent)
// treats Background as an opaque string interpolated into a Claude prompt — this exists
// specifically for the renderer (BackgroundData.Experience/Education/Credentials/Publications/
// Volunteering feed deterministic sections) and for reading Personal.Name directly instead of
// parsing CvBase's first markdown line.
public static class BackgroundYamlParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    // Never throws on a genuinely malformed document. This matters beyond the original
    // migration/backfill use case now: GET /auth/me calls this on nearly every authenticated
    // page load (for the dashboard's firstName greeting), so a parse exception here would be a
    // hot-path outage, not a one-time admin-tool failure. background.yaml was hand-authored/
    // AI-authored prose-adjacent text, not a strict API contract before this parser existed —
    // production data has already surfaced real shape mismatches (Stack/TechStack/Anchors,
    // fixed by dropping those fields from BackgroundData), and there's no guarantee every field
    // still in the schema is safe for every user's document. A parse failure falls through to
    // "nothing extractable" (empty BackgroundData), not a hard error.
    public static BackgroundData Parse(string yamlText)
    {
        if (string.IsNullOrWhiteSpace(yamlText)) return new BackgroundData();
        try
        {
            return Deserializer.Deserialize<BackgroundData>(yamlText) ?? new BackgroundData();
        }
        catch
        {
            return new BackgroundData();
        }
    }

    // Strips the top-level "personal" key (name/handle/email/phone/location/linkedin/github/
    // portfolio_url — see BackgroundData.Personal) out of the raw YAML text before it's used as
    // CV-tailoring prompt context. CvTailorAgent's tool-use tailoring calls only need the content
    // sections (experience/education/projects/skills/etc.) — the candidate's contact details are
    // never relevant to phrasing a bullet, and the final render always splices real contact info
    // in deterministically from the parsed BackgroundData, not from anything Claude sees or
    // returns (see ResumeRenderer.Render / CvTailorAgent.ApplyDeltaAndRender).
    //
    // Line-based rather than a full YamlDotNet round-trip deliberately: re-serializing arbitrary
    // hand/AI-authored YAML back out risks the same "dynamic dictionary deserialization throws on
    // an ambiguous shape" failure mode this class exists to avoid (see the class-level comment)
    // — for content this method doesn't even need to understand structurally. A YAML top-level
    // key always starts at column 0, so this only needs to find where the "personal" block starts
    // and ends, not parse its values. Never throws, same contract as Parse above: worst case (an
    // unexpected shape) it returns the text unchanged rather than faulting the tailoring call.
    public static string StripPersonalSection(string yamlText)
    {
        if (string.IsNullOrWhiteSpace(yamlText)) return yamlText;

        var lines = yamlText.Replace("\r\n", "\n").Split('\n');
        var sb = new StringBuilder();
        var skippingPersonal = false;
        foreach (var line in lines)
        {
            var isTopLevelLine = line.Length > 0 && !char.IsWhiteSpace(line[0]);
            if (isTopLevelLine)
                skippingPersonal = line.StartsWith("personal:", StringComparison.OrdinalIgnoreCase);

            if (!skippingPersonal)
                sb.Append(line).Append('\n');
        }
        return sb.ToString();
    }
}
