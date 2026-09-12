namespace JobSearch.Data;

// Builds the "source material" string AccuracyVerifierAgent.VerifyAsync checks generated content
// against. Split out of Program.cs's endpoint handlers (same principle as CvRevisionOutputValidator
// /CoverLetterOutputValidator) specifically so the redaction behavior below is unit-testable
// without a live API call or a WebApplicationFactory integration test.
//
// Every call site used to interpolate profile.Background (the candidate's raw YAML, contact
// fields included) directly into this string, which AccuracyVerifierAgent then sends to Claude —
// the same class of gap CvTailorAgent.GenerateAsync's BuildSystemPrompt already fixed for the
// CV-tailoring prompt (see BackgroundYamlParser.StripPersonalSection's own comment). Verification
// only needs to check that generated claims (tools, metrics, responsibilities) trace back to real
// experience/education/skills/projects — never the candidate's email/phone/location/linkedin/
// github, so the same strip-the-personal-block fix applies here unchanged, not a new mechanism.
public static class AccuracyVerifierSourceMaterial
{
    // CV/CV-revision verification: BACKGROUND yaml plus the rendered base CV, matching what
    // CvTailorAgent.BuildSystemPrompt itself shows the generator (see ResumeRenderer.Render's
    // includeContactInfo comment) — verifying against anything less or more would misrepresent
    // what the generator actually had to work with.
    public static string ForCv(string backgroundYaml, UserResume resume) =>
        $"{BackgroundYamlParser.StripPersonalSection(backgroundYaml)}\n\n--- BASE CV ---\n" +
        $"{ResumeRenderer.Render(BackgroundYamlParser.Parse(backgroundYaml), resume, isPromptContext: true, includeContactInfo: false)}";

    // Cover letter / answer verification: BackgroundYamlParser.StripPersonalSection is a
    // line-based text strip (not a structural round-trip — see its own comment), safe to apply
    // directly to the raw yaml with no BASE CV context needed.
    public static string ForBackgroundOnly(string backgroundYaml) =>
        BackgroundYamlParser.StripPersonalSection(backgroundYaml);
}
