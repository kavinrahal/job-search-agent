using System.Text.Json;
using JobSearch.Data;

namespace JobSearchAgent.Tests;

public class ResumeIntakeAgentTests
{
    private static JsonElement Json(string value) => JsonSerializer.SerializeToElement(value);

    // TC01 — requested field present → returned as-is.
    [Fact]
    public void ExtractField_FieldPresent_ReturnsValue()
    {
        var input = new Dictionary<string, JsonElement> { ["background_yaml"] = Json("name: Kavin") };

        var result = ResumeIntakeAgent.ExtractField(input, "background_yaml");

        Assert.Equal("name: Kavin", result);
    }

    // TC02 — requested field missing (the exact production incident: a response cut off by
    // MaxTokens never starts this field at all) throws a clear, diagnosable exception instead
    // of a bare KeyNotFoundException.
    [Fact]
    public void ExtractField_FieldMissing_ThrowsClearException()
    {
        var input = new Dictionary<string, JsonElement> { ["cv_base_markdown"] = Json("# Kavin") };

        var ex = Assert.Throws<InvalidOperationException>(
            () => ResumeIntakeAgent.ExtractField(input, "background_yaml"));
        Assert.Contains("background_yaml", ex.Message);
    }

    // TC03 — an unrelated field present alongside the requested one doesn't interfere; only the
    // requested key is looked up.
    [Fact]
    public void ExtractField_OtherFieldsPresent_StillFindsRequestedOne()
    {
        var input = new Dictionary<string, JsonElement>
        {
            ["background_yaml"] = Json("name: Kavin"),
            ["cv_base_markdown"] = Json("# Kavin"),
        };

        Assert.Equal("# Kavin", ResumeIntakeAgent.ExtractField(input, "cv_base_markdown"));
    }
}
