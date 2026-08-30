namespace AdminDashboard.Api.Services;

// Every Emergency action (credit adjust, tier change, deactivate, clear worker lock,
// maintenance toggle, banner toggle) requires typing "CONFIRM" before it submits. The client
// JS on the Emergency page also disables the submit button until this matches, but that check
// alone is not a safety control — it's trivially bypassed with devtools or a raw POST — so
// every OnPost handler calls this again server-side before touching the database.
public static class ConfirmTextValidator
{
    public const string RequiredText = "CONFIRM";

    // Case-sensitive, no trimming leniency beyond surrounding whitespace: this is a
    // safety-significant challenge-response, not a search box, so it should not silently
    // accept "confirm" or "Confirm " as if the operator had typed it carefully.
    public static bool IsValid(string? input) =>
        string.Equals(input?.Trim(), RequiredText, StringComparison.Ordinal);
}
