namespace JobSearch.Data;

// CvTailorAgent.ReviseAsync (unlike CvTailorAgent.GenerateAsync's forced tool-use flow) is a
// free-text call — see tailor_cv.md's own "Revision" contract: respond with the complete revised
// resume as plain markdown, in exactly the same format as CURRENT RESUME. Same failure mode as
// CoverLetterAgent's free-text calls (see CoverLetterOutputValidator's own comment): a refusal
// comes back as an ordinary 200 response instead of a thrown exception, so it isn't caught by
// WithCreditAsync's exception-based refund, and would otherwise be persisted/served as if it were
// a valid revised CV.
//
// A rendered resume (see ResumeRenderer.Render) always starts with "# {Name}" as its first line —
// that's the one structural invariant every revision shares, sparse background or not, so it's
// the primary check here rather than a word-count/section-count rule that a genuinely thin
// background could legitimately fall short of. Combined with the same refusal-signal phrases
// CoverLetterOutputValidator looks for and a low length floor against a degenerate one-word
// response, not the cover letter's 350-500-word/salutation rules, which don't apply to a CV.
public static class CvRevisionOutputValidator
{
    // A genuine resume, even a sparse one (a single role, no projects/education), comfortably
    // clears this — it exists to catch a degenerate one-line/one-word response, not to police
    // length. Deliberately far below a cover letter's floor since resumes vary hugely in size.
    public const int MinWords = 30;

    public static bool LooksLikeRevisedResume(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.Trim();

        // Matches ResumeRenderer.Render's own opening line ("# " + Personal.Name) — the one
        // shape every real revision has regardless of how sparse the underlying background is.
        if (!trimmed.StartsWith("# ")) return false;

        var wordCount = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount < MinWords) return false;

        if (RefusalSignalPhrases.AnyMatch(trimmed.ToLowerInvariant())) return false;

        return true;
    }
}
