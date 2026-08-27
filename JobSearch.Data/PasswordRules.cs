namespace JobSearch.Data;

// The actual enforcement gate for password strength — any client-side check is UX only, never
// trusted. Deliberately a fixed length-plus-character-class rule set, not a scored/zxcvbn-style
// checker, so the client-side mirror of this rule (JobSearch.Web's passwordRules.ts, added in
// the frontend PR) can state the same rule identically without the two drifting apart.
public static class PasswordRules
{
    public const int MinLength = 8;

    // Returns the human-readable rule(s) that failed, empty when the password satisfies all
    // of them. Order matches the rule list above so a 400 response lists failures consistently.
    public static List<string> Validate(string password)
    {
        var errors = new List<string>();
        if (password.Length < MinLength) errors.Add($"Must be at least {MinLength} characters.");
        if (!password.Any(char.IsLower)) errors.Add("Must include a lowercase letter.");
        if (!password.Any(char.IsUpper)) errors.Add("Must include an uppercase letter.");
        if (!password.Any(char.IsDigit)) errors.Add("Must include a number.");
        if (!password.Any(c => !char.IsLetterOrDigit(c))) errors.Add("Must include a special character.");
        return errors;
    }
}
