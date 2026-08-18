import type {
  Summary,
  EmailsResponse,
  ApplicationsResponse,
  ApplicationWithEvents,
  ActivityItem,
  DiscoveriesResponse,
  HealthStatus,
  ParsedResume,
  Profile,
  GenerationResult,
  PostingCandidate,
  SourcesResponse,
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
  if (!res.ok) {
    const errBody = await res.json().catch(() => null);
    throw new Error(errBody?.error ?? `${res.status} ${res.statusText}`);
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

export interface EmailsParams {
  page?: number;
  pageSize?: number;
  category?: string;
  jobRelatedOnly?: boolean;
  from?: string;
  to?: string;
}

export async function fetchEmails(params: EmailsParams = {}): Promise<EmailsResponse> {
  return request(`/emails${qs(params)}`);
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

export async function fetchActivity(limit = 30): Promise<ActivityItem[]> {
  return request(`/activity?limit=${limit}`);
}

export async function fetchHealth(): Promise<HealthStatus> {
  const res = await fetch(`${BASE}/health`, { credentials: "include" });
  // 503 means stale — still parse the body
  if (!res.ok && res.status !== 503) throw new Error(`${res.status} ${res.statusText}`);
  return res.json();
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
  needsOnboarding: boolean;
  needsSourceSelection: boolean;
  isOwner: boolean;
}> {
  return request("/auth/me");
}

export async function fetchSources(): Promise<SourcesResponse> {
  return request("/sources");
}

export async function updateSources(sources: string[]): Promise<{ enabled: string[] }> {
  return request("/sources", { method: "PUT", ...json({ sources }) });
}

export async function logout(): Promise<void> {
  await request("/auth/logout", { method: "POST" });
}

export async function cancelAccount(): Promise<void> {
  await request("/account/cancel", { method: "POST" });
}

// Beta-only, no payment gate — see the matching backend endpoint's comment.
export async function upgradeToTier2(): Promise<void> {
  await request("/account/upgrade-to-tier2", { method: "POST" });
}

// A real navigable link (redirects through Google's consent screen), not a fetch — same
// direct-URL pattern as resumePdfUrl.
export function gmailOAuthStartUrl(): string {
  return `${BASE}/gmail-oauth/start`;
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

// Company is a separate query param, not folded into the title search string — Jora/Adzuna's
// keyword search ranks worse when a company name is blended into the query. See
// JobFetcherUtils.RankByCompany for how the backend uses it instead (reorders results, doesn't
// search on it).
export async function searchPostingCandidates(title: string, company?: string): Promise<{ candidates: PostingCandidate[] }> {
  const companyParam = company ? `&company=${encodeURIComponent(company)}` : "";
  return request(`/postings/search-candidates?title=${encodeURIComponent(title)}${companyParam}`);
}

export async function generateCv(input: { postingUrl?: string; postingText?: string; postingTitle?: string; postingCompany?: string }): Promise<GenerationResult> {
  return request("/cv", { method: "POST", ...json(input) });
}

export async function generateLetter(input: { postingUrl?: string; postingText?: string; postingTitle?: string; postingCompany?: string }): Promise<GenerationResult> {
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
