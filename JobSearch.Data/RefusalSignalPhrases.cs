namespace JobSearch.Data;

// Shared by CoverLetterOutputValidator and CvRevisionOutputValidator — phrases that show up when
// a free-text writing agent narrates its own reasoning/limitations instead of producing the
// document itself, rather than throwing the way a forced tool-use call would (see
// CoverLetterOutputValidator's own comment for the incident this guards against). Matched
// case-insensitively anywhere in the text, not just the opening, since a refusal can still open
// in a shape that passes the caller's own structural check and explain itself in the body.
internal static class RefusalSignalPhrases
{
    public static readonly string[] Values =
    [
        "i notice the",
        "i can't write",
        "i cannot write",
        "i don't have enough",
        "i do not have enough",
        "i don't know whose",
        "i do not know whose",
        "the background file",
        "the skill's",
        "the skill instructions",
        "these don't match",
        "these do not match",
        "as an ai",
        "i'm an ai",
        "i am an ai",
        "system prompt",
        "candidate background provided",
    ];

    public static bool AnyMatch(string lowerText) => Values.Any(lowerText.Contains);
}
