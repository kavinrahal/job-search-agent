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
}
