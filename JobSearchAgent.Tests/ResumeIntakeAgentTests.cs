using System.Text.Json;
using JobSearch.Data;

namespace JobSearchAgent.Tests;

public class ResumeIntakeAgentTests
{
    private static JsonElement Json(string value) => JsonSerializer.SerializeToElement(value);

    // TC01 — both required fields present → both come through on the result.
    [Fact]
    public void ExtractParsedResume_BothFieldsPresent_ReturnsBoth()
    {
        var input = new Dictionary<string, JsonElement>
        {
            ["background_yaml"] = Json("name: Kavin"),
            ["cv_base_markdown"] = Json("# Kavin"),
        };

        var result = ResumeIntakeAgent.ExtractParsedResume(input);

        Assert.Equal("name: Kavin", result.Background);
        Assert.Equal("# Kavin", result.CvBase);
    }

    // TC02 — missing background_yaml (the exact production incident: a response cut off by
    // MaxTokens never starts this field at all) throws a clear, diagnosable exception instead
    // of a bare KeyNotFoundException.
    [Fact]
    public void ExtractParsedResume_MissingBackgroundYaml_ThrowsClearException()
    {
        var input = new Dictionary<string, JsonElement>
        {
            ["cv_base_markdown"] = Json("# Kavin"),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ResumeIntakeAgent.ExtractParsedResume(input));
        Assert.Contains("missing a required field", ex.Message);
    }

    // TC03 — missing cv_base_markdown, the other half of the same failure mode.
    [Fact]
    public void ExtractParsedResume_MissingCvBaseMarkdown_ThrowsClearException()
    {
        var input = new Dictionary<string, JsonElement>
        {
            ["background_yaml"] = Json("name: Kavin"),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ResumeIntakeAgent.ExtractParsedResume(input));
        Assert.Contains("missing a required field", ex.Message);
    }
}
