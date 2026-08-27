using JobSearch.Data;

namespace JobSearch.Api;

// Request bodies for the CV/letter/answer/edit generation endpoints.
// PostingTitle/PostingCompany: used only if PostingUrl fails to fetch directly — searched
// against Jora/Adzuna as a cross-check (same mechanism as the Seek email-alert pipeline),
// since a bare URL alone carries no title/company to search with. Kept as separate fields
// rather than one combined hint because Jora/Adzuna's keyword search ranks worse when a
// company name is blended into the search query — see SearchCandidatesAsync in Program.cs.
public record GenerateRequest(int? DiscoveryId, string? PostingText, string? PostingUrl, string? PostingTitle, string? PostingCompany);
public record AnswerRequest(string Question, int? DiscoveryId, string? PostingUrl, string? PostingTitle, string? PostingCompany);
public record EditRequest(string Message);

// The three dependencies behind the Seek cross-check always travel together (they only ever
// get forwarded into TryCrossCheckAsync in Program.cs) — bundled so ResolvePostingTextAsync/
// GenerateArtifactAsync take one parameter for this concern instead of three.
public record CrossCheckDeps(JoraFetcher Jora, AdzunaFetcher? Adzuna, PostingMatcherAgent Matcher);

// PUT /api/v1/profile — only provided (non-null) fields are updated.
public record ProfileUpdateRequest(string? Background, string? CvBase, string? JobCriteria);

// POST /api/v1/support
public record SupportMessageRequest(string Message);

// PUT /api/v1/sources
public record SourcesUpdateRequest(string[] Sources);

// POST /api/v1/admin/invite
public record InviteRequest(string Email);

// POST /api/v1/auth/register
public record RegisterRequest(string Email, string Password);

// POST /api/v1/auth/login
public record LoginRequest(string Email, string Password);

// POST /api/v1/auth/forgot-password
public record ForgotPasswordRequest(string Email);

// POST /api/v1/auth/reset-password
public record ResetPasswordRequest(string Token, string NewPassword);

// POST /api/v1/account/cancel — the user's own explicit choice, not a default we pick for
// them. See the endpoint's own comment for exactly what this does and doesn't control.
public record CancelAccountRequest(bool DeleteData);

// POST /api/v1/applications — manually logging an application (the only creation path
// besides ApplicationTracker's email-driven one). CompanyDomain is only meaningful in
// filter tracking mode (see GmailTrackingMode) — installs a per-company Gmail filter.
public record CreateApplicationRequest(string Company, string RoleTitle, string? JobUrl, string? CompanyDomain);

// PATCH /api/v1/applications/{id}
public record UpdateApplicationStatusRequest(string Status);

// PUT /api/v1/gmail-tracking-mode
public record GmailTrackingModeRequest(string Mode);

// PUT /api/v1/resume — only provided (non-null) fields are updated, same partial-update shape
// as ProfileUpdateRequest above. SectionConfig/ExperienceOverrides/ProjectOverrides/SkillsSection
// here (not the raw JSON string) since the frontend builder edits these as structured objects,
// not free text — same shapes as UserResume's own JSON columns (see UserResumeData.cs).
// POST /api/v1/resume/preview accepts this exact same shape (the full draft, not yet saved).
public record ResumeUpdateRequest(
    string? Summary,
    List<SectionConfigEntry>? SectionConfig,
    List<ExperienceOverride>? ExperienceOverrides,
    List<ProjectOverride>? ProjectOverrides,
    List<SkillsSectionEntry>? SkillsSection);

// POST /api/v1/resume/apply-template — Seniority is "junior" or "experienced" (validated against
// ResumeSeniority in Program.cs); omitted/ignored for industries where
// ResumeIndustryTemplate.HasSeniorityToggle is false.
public record ApplyResumeTemplateRequest(string IndustryKey, string? Seniority);
