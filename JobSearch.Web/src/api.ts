import type {
  Summary,
  Application,
  ApplicationsResponse,
  ApplicationWithEvents,
  ActivityItem,
  DiscoveriesResponse,
  ParsedResume,
  Profile,
  GenerationResult,
  PostingCandidate,
  SourcesResponse,
  ResumeData,
  ResumeTemplatesResponse,
  ResumePreviewResponse,
} from "./types";

// VITE_API_URL is set in production to the API's own Railway URL — the frontend and API are
// separate deployments (separate origins), so credentials: "include" below is required on
// every request for the session cookie to actually be sent.
// In local dev it's unset and Vite's proxy forwards /api to localhost:5000, making dev
// same-origin from the browser's point of view.
const BASE = (import.meta.env.VITE_API_URL ?? "") + "/api/v1";

export class InsufficientCreditsError extends Error {}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const res = await fetch(`${BASE}${path}`, { ...options, credentials: "include" });
  if (res.status === 402) throw new InsufficientCreditsError("Insufficient credits");
  // Every protected endpoint 401s via the same RequireAuthorization() cookie challenge (see
  // Program.cs) — mid-visit, that only means the session cookie died (expired, cleared,
  // cancelled elsewhere), which no page can recover from on its own (each page's hook has no way
  // to tell App.tsx's `me` is now stale). Hard-navigate rather than throw, same "full reload, not
  // a client navigate" pattern logout/cancel/upgrade already use, so the next /auth/me read
  // starts clean. /auth/me itself is exempt: a 401 there is the normal signal for "not logged in
  // yet" on first load, not a dead session, and redirecting on it would loop at "/".
  if (res.status === 401 && path !== "/auth/me") {
    window.location.href = "/";
    return new Promise<T>(() => {});
  }
  if (!res.ok) {
    const errBody = await res.json().catch(() => null);
    // Two error shapes exist server-side: the usual `{ error: "..." }` single message, and
    // `{ errors: [...] }` — a list of every failed password rule, returned by /auth/register
    // and /auth/reset-password (see PasswordRules.Validate). Join the list into one sentence
    // so every caller keeps dealing with a single Error.message either way.
    const ruleErrors: string | null = Array.isArray(errBody?.errors) ? errBody.errors.join(" ") : null;
    throw new Error(errBody?.error ?? ruleErrors ?? `${res.status} ${res.statusText}`);
  }
  // Some endpoints reply 200 with an empty body (Results.Ok() with no value) rather than 204
  // — checking status alone isn't enough, so parse whatever text actually came back instead.
  const text = await res.text();
  return text ? JSON.parse(text) : (undefined as T);
}

function json(body: object): RequestInit {
  return { headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) };
}

function qs(params: object): string {
  const q = new URLSearchParams();
  for (const [k, v] of Object.entries(params))
    if (v != null && v !== false) q.set(k, String(v));
  const s = q.toString();
  return s ? "?" + s : "";
}

export async function fetchSummary(): Promise<Summary> {
  return request("/summary");
}

export async function fetchApplications(params: {
  status?: string;
  page?: number;
  pageSize?: number;
} = {}): Promise<ApplicationsResponse> {
  return request(`/applications${qs(params)}`);
}

export async function fetchApplicationEvents(id: number): Promise<ApplicationWithEvents> {
  return request(`/applications/${id}/events`);
}

// The manual counterpart to the automatic email-driven tracker — companyDomain is only
// meaningful in filter tracking mode (installs a per-company Gmail filter server-side).
export async function createApplication(input: {
  company: string;
  roleTitle: string;
  jobUrl?: string;
  companyDomain?: string;
  status?: string;
}): Promise<Application> {
  return request("/applications", { method: "POST", ...json(input) });
}

export async function updateApplicationStatus(id: number, status: string): Promise<{ id: number; status: string; updatedAt: string }> {
  return request(`/applications/${id}`, { method: "PATCH", ...json({ status }) });
}

export async function fetchActivity(limit = 30): Promise<ActivityItem[]> {
  return request(`/activity?limit=${limit}`);
}

export async function fetchDiscoveries(params: {
  recommendation?: string;
  page?: number;
  pageSize?: number;
} = {}): Promise<DiscoveriesResponse> {
  return request(`/discoveries${qs(params)}`);
}

export async function fetchMe(): Promise<{
  email: string;
  tier: string;
  creditBalance: number;
  needsOnboarding: boolean;
  needsCriteria: boolean;
  needsSourceSelection: boolean;
  isOwner: boolean;
  firstName: string | null;
}> {
  return request("/auth/me");
}

export async function fetchSources(): Promise<SourcesResponse> {
  return request("/sources");
}

export async function updateSources(sources: string[]): Promise<{ enabled: string[] }> {
  return request("/sources", { method: "PUT", ...json({ sources }) });
}

export async function updateGmailTrackingMode(mode: "full" | "filter" | "manual"): Promise<{ gmailTrackingMode: string }> {
  return request("/gmail-tracking-mode", { method: "PUT", ...json({ mode }) });
}

export async function logout(): Promise<void> {
  await request("/auth/logout", { method: "POST" });
}

// --- Email/password auth. A second, independent path alongside Google OAuth (which stays a
// plain navigable URL — see useLoginUrl — not a fetch, because it's a redirect round-trip).
// Every one of these signs the user in via the same session cookie Google's callback sets, so
// on success the caller needs a fresh /auth/me: hard-navigate rather than client-side route.

// Two success outcomes, deliberately distinguished by the server: "signed_in" when this email
// already proved ownership through a prior Google login (registration attaches to that same
// account and needs no re-verification), "verification_sent" for a genuinely new account, which
// is NOT signed in until the emailed link is clicked.
export async function register(email: string, password: string): Promise<{ status: "signed_in" | "verification_sent" }> {
  return request("/auth/register", { method: "POST", ...json({ email, password }) });
}

// Note the path is shared with Google's GET /auth/login challenge endpoint — same route,
// different verb, so this POST doesn't collide with it.
export async function login(email: string, password: string): Promise<void> {
  await request("/auth/login", { method: "POST", ...json({ email, password }) });
}

// Always resolves with the same generic message whether or not the email has an account —
// the server refuses to confirm either way, to prevent account enumeration.
export async function requestPasswordReset(email: string): Promise<{ message: string }> {
  return request("/auth/forgot-password", { method: "POST", ...json({ email }) });
}

// The token comes from the ?resetToken= param on the emailed link. Signs the user in on success.
export async function resetPassword(token: string, newPassword: string): Promise<void> {
  await request("/auth/reset-password", { method: "POST", ...json({ token, newPassword }) });
}

export async function cancelAccount(deleteData: boolean): Promise<void> {
  await request("/account/cancel", { method: "POST", ...json({ deleteData }) });
}

// Beta-only, no payment gate — see the matching backend endpoint's comment.
export async function upgradeToTier2(): Promise<void> {
  await request("/account/upgrade-to-tier2", { method: "POST" });
}

// A real navigable link (redirects through Google's consent screen), not a fetch — same
// direct-URL pattern as resumePdfUrl. mode "full" requests gmail.readonly instead of the
// default gmail.settings.basic (see the matching backend endpoint's comment).
export function gmailOAuthStartUrl(mode?: "full"): string {
  return `${BASE}/gmail-oauth/start${mode ? `?mode=${mode}` : ""}`;
}

export interface GmailForwardingStatusResponse {
  address: string;
  status: "not_added" | "pending" | "verified";
  filterInstalled: boolean;
}

// Reads the user's own forwarding-address status back from Gmail and, once verified,
// installs the job-alert filter — see the matching backend endpoint's comment for why
// this can't just create the forwarding address itself (a Google API restriction).
export async function fetchGmailForwardingStatus(): Promise<GmailForwardingStatusResponse> {
  return request("/gmail-forwarding-status");
}

// Owner only. Adds the email to the beta invite list and emails them if SendGrid Mail Send
// is configured — see the matching backend endpoint's comment.
export async function inviteToTier2(email: string): Promise<{ email: string; emailSent: boolean }> {
  return request("/admin/invite", { method: "POST", ...json({ email }) });
}

export async function submitSupportMessage(message: string): Promise<void> {
  await request("/support", { method: "POST", ...json({ message }) });
}

export async function parseResumePdf(file: File): Promise<ParsedResume> {
  const form = new FormData();
  form.set("file", file);
  return request("/onboarding/parse-resume", { method: "POST", body: form });
}

// Works the same way threadPdfUrl does — a direct URL for <embed>/<iframe> src or download,
// relying on the browser's own session cookie rather than a fetched blob.
export function resumePdfUrl(): string {
  return `${BASE}/profile/resume-pdf`;
}

// Called at save time, not at parse time — see the comment on the matching backend endpoint.
export async function uploadResumePdf(file: File): Promise<void> {
  const form = new FormData();
  form.set("file", file);
  await request("/profile/resume-pdf", { method: "POST", body: form });
}

export async function fetchProfile(): Promise<Profile> {
  return request("/profile");
}

export async function updateProfile(
  fields: Partial<Pick<Profile, "background" | "cvBase" | "jobCriteria">>,
): Promise<Profile> {
  return request("/profile", { method: "PUT", ...json(fields) });
}

// Static catalog, not per-user — safe to call before /resume has ever succeeded.
export async function fetchResumeTemplates(): Promise<ResumeTemplatesResponse> {
  return request("/resume-templates");
}

// Throws (409, "Resume setup isn't finished yet…") until UserResume exists for this user —
// see the matching backend endpoint's comment.
export async function fetchResume(): Promise<ResumeData> {
  return request("/resume");
}

// Shared by updateResume and fetchResumePreview — both send this same partial-draft shape
// (only provided fields are considered; PUT persists them, /preview renders them transiently).
export type ResumeDraft = Partial<Pick<
  ResumeData, "summary" | "sectionConfig" | "experienceOverrides" | "projectOverrides" | "skillsSection"
>>;

export async function updateResume(fields: ResumeDraft): Promise<ResumeData> {
  return request("/resume", { method: "PUT", ...json(fields) });
}

// Renders the *real* ResumeRenderer output against a draft that hasn't been saved yet — no DB
// write happens server-side (see POST /resume/preview's own comment). Debounced by the caller
// (useDebouncedPreview), not here, so this stays a plain one-shot request.
export async function fetchResumePreview(draft: ResumeDraft): Promise<ResumePreviewResponse> {
  return request("/resume/preview", { method: "POST", ...json(draft) });
}

export async function applyResumeTemplate(industryKey: string, seniority?: "junior" | "experienced"): Promise<ResumeData> {
  return request("/resume/apply-template", { method: "POST", ...json({ industryKey, seniority }) });
}

// Draft only — does not persist. Fills the caller's (unsaved) summary field; the page's normal
// Save button still owns writing it. 422s with a clear message if Background has nothing to
// summarize yet (see the matching backend endpoint's comment).
export async function generateResumeSummary(): Promise<{ summary: string }> {
  return request("/resume/generate-summary", { method: "POST" });
}

// Company is a separate query param, not folded into the title search string — Jora/Adzuna's
// keyword search ranks worse when a company name is blended into the query. See
// JobFetcherUtils.RankByCompany for how the backend uses it instead (reorders results, doesn't
// search on it).
export async function searchPostingCandidates(title: string, company?: string): Promise<{ candidates: PostingCandidate[] }> {
  const companyParam = company ? `&company=${encodeURIComponent(company)}` : "";
  return request(`/postings/search-candidates?title=${encodeURIComponent(title)}${companyParam}`);
}

// discoveryId is the Discover tab's one-tap path: the backend resolves the posting text from
// the discovery record itself (cached at discovery time — see DiscoveredPosting.PostingText),
// so nothing has to be re-fetched from a job board that may block us by then.
export interface GenerateInput {
  discoveryId?: number;
  postingUrl?: string;
  postingText?: string;
  postingTitle?: string;
  postingCompany?: string;
}

export async function generateCv(input: GenerateInput): Promise<GenerationResult> {
  return request("/cv", { method: "POST", ...json(input) });
}

export async function generateLetter(input: GenerateInput): Promise<GenerationResult> {
  return request("/letter", { method: "POST", ...json(input) });
}

export async function askQuestion(input: { question: string; postingUrl?: string; postingTitle?: string; postingCompany?: string }): Promise<GenerationResult> {
  return request("/answer", { method: "POST", ...json(input) });
}

export async function editThread(threadId: number, message: string): Promise<GenerationResult> {
  return request(`/threads/${threadId}/edit`, { method: "POST", ...json({ message }) });
}

// Works for both CV and cover-letter threads — the endpoint renders based on the thread's
// own stored artifact type.
export function threadPdfUrl(threadId: number): string {
  return `${BASE}/threads/${threadId}/pdf`;
}

export function threadDocxUrl(threadId: number): string {
  return `${BASE}/threads/${threadId}/docx`;
}
