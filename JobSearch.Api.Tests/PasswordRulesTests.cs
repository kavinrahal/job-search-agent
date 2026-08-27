using JobSearch.Data;

namespace JobSearch.Api.Tests;

public class PasswordRulesTests
{
    // TC01 — A password satisfying every rule passes with no errors.
    [Fact]
    public void Validate_MeetsAllRules_ReturnsNoErrors()
    {
        var errors = PasswordRules.Validate("Abcdef1!");

        Assert.Empty(errors);
    }

    // TC02 — Too short is rejected even if every character class is present.
    [Fact]
    public void Validate_TooShort_ReturnsLengthError()
    {
        var errors = PasswordRules.Validate("Ab1!");

        Assert.Contains(errors, e => e.Contains("8 characters"));
    }

    // TC03 — Missing a lowercase letter is rejected.
    [Fact]
    public void Validate_NoLowercase_ReturnsLowercaseError()
    {
        var errors = PasswordRules.Validate("ABCDEF1!");

        Assert.Contains(errors, e => e.Contains("lowercase"));
    }

    // TC04 — Missing an uppercase letter is rejected.
    [Fact]
    public void Validate_NoUppercase_ReturnsUppercaseError()
    {
        var errors = PasswordRules.Validate("abcdef1!");

        Assert.Contains(errors, e => e.Contains("uppercase"));
    }

    // TC05 — Missing a digit is rejected.
    [Fact]
    public void Validate_NoDigit_ReturnsDigitError()
    {
        var errors = PasswordRules.Validate("Abcdefg!");

        Assert.Contains(errors, e => e.Contains("number"));
    }

    // TC06 — Missing a special character is rejected.
    [Fact]
    public void Validate_NoSpecialCharacter_ReturnsSpecialCharacterError()
    {
        var errors = PasswordRules.Validate("Abcdefg1");

        Assert.Contains(errors, e => e.Contains("special character"));
    }

    // TC07 — A password failing multiple rules at once reports all of them, not just the first.
    [Fact]
    public void Validate_FailsMultipleRules_ReturnsAllApplicableErrors()
    {
        var errors = PasswordRules.Validate("abc");

        Assert.Equal(4, errors.Count); // too short, no uppercase, no digit, no special char (has lowercase)
    }
}
