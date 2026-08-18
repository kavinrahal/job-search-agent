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
