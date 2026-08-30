using AdminDashboard.Api.Services;

namespace AdminDashboard.Api.Tests;

public class ConfirmTextValidatorTests
{
    [Fact]
    public void ExactMatch_IsValid()
    {
        Assert.True(ConfirmTextValidator.IsValid("CONFIRM"));
    }

    [Theory]
    [InlineData("confirm")]
    [InlineData("Confirm")]
    [InlineData("CONFIRMED")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("yes")]
    public void AnythingOtherThanExactConfirm_IsInvalid(string? input)
    {
        // Case and content must match exactly — this is a safety-significant
        // challenge-response, not a fuzzy search box.
        Assert.False(ConfirmTextValidator.IsValid(input));
    }

    [Fact]
    public void SurroundingWhitespace_IsTrimmedAndStillValid()
    {
        Assert.True(ConfirmTextValidator.IsValid("  CONFIRM  "));
    }
}
