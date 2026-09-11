using YamlDotNet.Serialization;

namespace JobSearch.Data;

// Extracts a single geographic location string from the user's JobCriteria YAML's `location`
// block (see JobCriteriaEditor.tsx's Location section / jobCriteriaYaml.ts's serializer) for
// Adzuna's `where` search parameter — replacing the previous hardcoded "melbourne" every user's
// proactive discovery sweep ran against regardless of where they actually are.
//
// Prefers the first selected state/region: Adzuna's `where` is a free-text place lookup, and a
// state name (e.g. "Victoria") narrows results usefully, while a bare country name (e.g.
// "Australia") only returns country-wide results — still better than nothing, so it's the
// fallback when no state is given. `locationNotes` (free-text "city preference, or lack
// thereof") is deliberately not used here: unlike countries/states (validated dropdown data,
// or at minimum single-purpose free text), it's an open note field that can contain anything
// from a city name to "no preference" to a full sentence, too unreliable to feed an API
// location param directly.
//
// Same never-throws contract as BackgroundYamlParser: JobCriteria is user-authored/AI-touched
// YAML, not a strict contract, so a shape mismatch degrades to "no location found" (the caller
// falls back to its own default) rather than an exception on a hot discovery-worker path.
public static class JobLocation
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static string? Parse(string? jobCriteria)
    {
        if (string.IsNullOrWhiteSpace(jobCriteria)) return null;

        try
        {
            var location = Deserializer.Deserialize<CriteriaRoot>(jobCriteria)?.Location ?? new LocationBlock();

            return FirstNonEmpty(location.States) ?? FirstNonEmpty(location.Countries);
        }
        catch
        {
            return null;
        }
    }

    private static string? FirstNonEmpty(List<string> values) =>
        values.Select(v => v?.Trim()).FirstOrDefault(v => !string.IsNullOrEmpty(v));

    private sealed class CriteriaRoot
    {
        public LocationBlock Location { get; set; } = new();
    }

    private sealed class LocationBlock
    {
        public List<string> States { get; set; } = [];
        public List<string> Countries { get; set; } = [];
    }
}
