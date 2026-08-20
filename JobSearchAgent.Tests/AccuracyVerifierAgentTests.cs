using System.Text.Json;
using JobSearch.Data;

namespace JobSearchAgent.Tests;

public class AccuracyVerifierAgentTests
{
    private static JsonElement Json<T>(T value) => JsonSerializer.SerializeToElement(value);

    // TC01 — the common case: some claims flagged, returned as-is.
    [Fact]
    public void ExtractFlaggedClaims_ClaimsPresent_ReturnsThem()
    {
        var input = new Dictionary<string, JsonElement>
        {
            ["flagged_claims"] = Json(new[] { "5 years of Kubernetes experience", "Team of 20 engineers" }),
        };

        var result = AccuracyVerifierAgent.ExtractFlaggedClaims(input);

        Assert.Equal(["5 years of Kubernetes experience", "Team of 20 engineers"], result);
    }

    // TC02 — the expected clean-content case: an explicit empty array, not absence of the key.
    [Fact]
    public void ExtractFlaggedClaims_EmptyArray_ReturnsEmpty()
    {
        var input = new Dictionary<string, JsonElement> { ["flagged_claims"] = Json(Array.Empty<string>()) };

        Assert.Empty(AccuracyVerifierAgent.ExtractFlaggedClaims(input));
    }

    // TC03 — key missing entirely (a malformed/truncated tool call) — fails safe to empty
    // rather than throwing, since this runs as a non-blocking side check; a parsing error here
    // must never take down the actual generation result it's checking.
    [Fact]
    public void ExtractFlaggedClaims_KeyMissing_ReturnsEmptyWithoutThrowing()
    {
        var input = new Dictionary<string, JsonElement> { ["other_field"] = Json("value") };

        Assert.Empty(AccuracyVerifierAgent.ExtractFlaggedClaims(input));
    }

    // TC04 — blank/whitespace-only entries are dropped rather than surfaced as empty warnings
    // in the UI.
    [Fact]
    public void ExtractFlaggedClaims_BlankEntries_Filtered()
    {
        var input = new Dictionary<string, JsonElement>
        {
            ["flagged_claims"] = Json(new[] { "Real claim", "", "   " }),
        };

        Assert.Equal(["Real claim"], AccuracyVerifierAgent.ExtractFlaggedClaims(input));
    }
}
