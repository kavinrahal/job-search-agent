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

// Tests AccuracyVerifierSourceMaterial — the fix for the "AccuracyVerifierAgent's source material
// still includes full unredacted contact info" finding. Every Program.cs call site used to
// interpolate profile.Background (raw yaml, contact fields included) straight into the string
// sent to Claude for verification; these assert the redaction actually happens, same pattern as
// BackgroundYamlParserTests' StripPersonalSection coverage.
public class AccuracyVerifierSourceMaterialTests
{
    private const string BackgroundYaml = """
        personal:
          name: Jordan Rivers
          email: jordan.rivers@example.com
          phone: "555-0199"
          location: Springfield, IL
          linkedin: linkedin.com/in/jordanrivers
          github: github.com/jordanrivers
        experience:
          - company: Acme
            role: Engineer
        """;

    private static UserResume BaseResume() => new()
    {
        Summary = "A summary.",
        SectionConfigJson = "[]",
        ExperienceOverridesJson = "[]",
        SkillsSectionJson = "[]",
        ProjectOverridesJson = "[]",
    };

    [Fact]
    public void ForCv_RedactsContactFieldsFromBothBackgroundAndRenderedBaseCv()
    {
        var sourceMaterial = AccuracyVerifierSourceMaterial.ForCv(BackgroundYaml, BaseResume());

        Assert.DoesNotContain("jordan.rivers@example.com", sourceMaterial);
        Assert.DoesNotContain("555-0199", sourceMaterial);
        Assert.DoesNotContain("Springfield, IL", sourceMaterial);
        Assert.DoesNotContain("linkedin.com/in/jordanrivers", sourceMaterial);
        Assert.DoesNotContain("github.com/jordanrivers", sourceMaterial);
        // The content verification actually needs still made it through.
        Assert.Contains("Acme", sourceMaterial);
        Assert.Contains("--- BASE CV ---", sourceMaterial);
    }

    [Fact]
    public void ForBackgroundOnly_RedactsContactFields()
    {
        var sourceMaterial = AccuracyVerifierSourceMaterial.ForBackgroundOnly(BackgroundYaml);

        Assert.DoesNotContain("jordan.rivers@example.com", sourceMaterial);
        Assert.DoesNotContain("555-0199", sourceMaterial);
        Assert.DoesNotContain("Springfield, IL", sourceMaterial);
        Assert.DoesNotContain("linkedin.com/in/jordanrivers", sourceMaterial);
        Assert.DoesNotContain("github.com/jordanrivers", sourceMaterial);
        Assert.Contains("Acme", sourceMaterial);
    }
}
