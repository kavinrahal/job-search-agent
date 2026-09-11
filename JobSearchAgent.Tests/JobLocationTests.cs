using JobSearch.Data;

namespace JobSearchAgent.Tests;

public class JobLocationTests
{
    // TC01 — States present (as js-yaml block-sequences them under location.states) → first
    // state returned, the more specific value Adzuna's `where` should search against.
    [Fact]
    public void Parse_StatesPresent_ReturnsFirstState()
    {
        var yaml = """
            location:
              countries:
                - Australia
              states:
                - Victoria
                - New South Wales
              notes: ''
            """;

        Assert.Equal("Victoria", JobLocation.Parse(yaml));
    }

    // TC02 — No states given, but countries present → falls back to the first country.
    [Fact]
    public void Parse_NoStates_FallsBackToFirstCountry()
    {
        var yaml = """
            location:
              countries:
                - Australia
              states: []
              notes: ''
            """;

        Assert.Equal("Australia", JobLocation.Parse(yaml));
    }

    // TC03 — `location` key absent entirely (old criteria saved before this field existed, or
    // hand-edited YAML that dropped it) → null, not a throw.
    [Fact]
    public void Parse_LocationKeyMissing_ReturnsNull()
    {
        var yaml = "target_job_titles: Software Engineer\nemployment_type_preference:\n  - full_time";

        Assert.Null(JobLocation.Parse(yaml));
    }

    // TC04 — Both countries and states present but empty (the "user opened Location and left
    // everything blank" case) → null so the caller falls through to its own default.
    [Fact]
    public void Parse_EmptyLists_ReturnsNull()
    {
        var yaml = "location:\n  countries: []\n  states: []\n";

        Assert.Null(JobLocation.Parse(yaml));
    }

    // TC05 — Null/empty input criteria (blank profile) → null, not a throw.
    [Fact]
    public void Parse_NullOrEmptyCriteria_ReturnsNull()
    {
        Assert.Null(JobLocation.Parse(null));
        Assert.Null(JobLocation.Parse(""));
    }

    // TC06 — Malformed/unexpected-shape YAML (e.g. `location` is a plain scalar instead of a
    // mapping — could happen from hand-edited or AI-touched criteria text) never throws; it
    // degrades to "nothing found" like BackgroundYamlParser does for the same reason.
    [Fact]
    public void Parse_MalformedShape_ReturnsNullWithoutThrowing()
    {
        var yaml = "location: \"just some free text, not a mapping\"";

        var ex = Record.Exception(() => JobLocation.Parse(yaml));

        Assert.Null(ex);
        Assert.Null(JobLocation.Parse(yaml));
    }

    // TC07 — Whitespace-only entries in the list are skipped in favor of the first real value.
    [Fact]
    public void Parse_BlankFirstEntry_SkipsToNextNonEmpty()
    {
        var yaml = "location:\n  states:\n    - ''\n    - Queensland\n";

        Assert.Equal("Queensland", JobLocation.Parse(yaml));
    }
}
