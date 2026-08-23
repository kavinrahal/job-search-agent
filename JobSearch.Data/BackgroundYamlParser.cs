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

    // Never throws on a genuinely malformed document — the migration/backfill path (which is
    // where "existing user's YAML doesn't quite parse" would actually surface) treats a parse
    // failure as "nothing extractable" and falls through to defaults, not a hard error, since
    // background.yaml was hand-authored/AI-authored prose-adjacent text, not a strict API
    // contract before this parser existed.
    public static BackgroundData Parse(string yamlText)
    {
        if (string.IsNullOrWhiteSpace(yamlText)) return new BackgroundData();
        return Deserializer.Deserialize<BackgroundData>(yamlText) ?? new BackgroundData();
    }
}
