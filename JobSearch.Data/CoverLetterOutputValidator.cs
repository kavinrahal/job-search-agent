namespace JobSearch.Data;

// CoverLetterAgent (unlike PostingEvaluator/AnswerAgent/CvTailorAgent's initial generation) is a
// free-text call, not a forced tool-use call — see architecture-conventions.md on why forced
// tool-use is preferred where the output is deserialized. A cover letter's whole output *is* free
// text, so there's no schema to force here. That means a refusal doesn't come back as a thrown
// exception the way a missing tool-use block would (see PostingEvaluator.EvaluateAsync's own
// "did not return a tool use block" throw) — it comes back as an ordinary 200 response containing
// the model's own reasoning about why it couldn't write the letter, indistinguishable at the
// HTTP-call level from a real letter.
//
// Real incident: a near-empty candidate Background produced output starting "I notice the
// background file I've been given is essentially empty... I can't write a credible, specific
// cover letter without that content..." — no exception, so it was saved as an ordinary successful
// AgentThread and served via the PDF/Word download endpoints, and the credit for it was never
// refunded (WithCreditAsync in Program.cs only refunds on a thrown exception).
//
// This is a heuristic floor, not a rewrite of write_cover_letter.md's own rules (350-500 words,
// salutation format) — it exists to catch a response that plainly isn't a letter at all, not to
// police writing quality.
public static class CoverLetterOutputValidator
{
    // Comfortably outside the skill's own 350-500 word rule in both directions — wide enough
    // that a legitimate letter (including a revision) never trips this, tight enough to catch
    // the degenerate cases actually seen in production: a one-word response ("To" — see
    // CoverLetterAgent's own comment on the historical Sonnet incident) and a multi-paragraph
    // refusal explanation, which tends to run long because it explains its own reasoning.
    public const int MinWords = 120;
    public const int MaxWords = 700;

    private static readonly string[] ValidSalutationPrefixes =
    [
        "dear ",
        "to the hiring manager,",
    ];

    // Phrases that show up in the model narrating its own reasoning/limitations rather than
    // writing the letter itself. Matched case-insensitively anywhere in the text, not just the
    // opening, since a refusal can still open with a salutation-shaped line and then explain
    // itself in the body.
    private static readonly string[] RefusalSignals =
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

    public static bool LooksLikeCoverLetter(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.Trim();
        var lower = trimmed.ToLowerInvariant();

        if (!ValidSalutationPrefixes.Any(lower.StartsWith)) return false;

        var wordCount = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount < MinWords || wordCount > MaxWords) return false;

        if (RefusalSignals.Any(lower.Contains)) return false;

        return true;
    }
}
