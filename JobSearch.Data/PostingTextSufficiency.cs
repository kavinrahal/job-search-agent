namespace JobSearch.Data;

// A genuine job posting is always well over a token or two of text. Content under this floor is
// a signal that a URL fetch "succeeded" (no throw) without actually returning a usable posting —
// a bot-block page, a login wall, a mostly-empty redirect target. Rejecting it here, before it
// ever reaches a CV/letter-writing agent, stops Claude from being handed nothing to work with and
// answering with a prose apology that would otherwise be saved and returned as an ordinary
// successful generation (see GenerateArtifactAsync in Program.cs).
public static class PostingTextSufficiency
{
    public const int MinLength = 200;

    public static bool IsSufficient(string? text) =>
        !string.IsNullOrWhiteSpace(text) && text.Trim().Length >= MinLength;
}
