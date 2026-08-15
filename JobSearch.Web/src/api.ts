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
  return res.status === 204 ? (undefined as T) : res.json();
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

export async function fetchMe(): Promise<{ email: string; needsOnboarding: boolean }> {
  return request("/auth/me");
}

export async function logout(): Promise<void> {
  await request("/auth/logout", { method: "POST" });
}

export async function cancelAccount(): Promise<void> {
  await request("/account/cancel", { method: "POST" });
}

export async function submitSupportMessage(message: string): Promise<void> {
  await request("/support", { method: "POST", ...json({ message }) });
}

export async function parseResumeText(text: string): Promise<ParsedResume> {
  const form = new FormData();
  form.set("text", text);
  return request("/onboarding/parse-resume", { method: "POST", body: form });
}

export async function parseResumePdf(file: File): Promise<ParsedResume> {
  const form = new FormData();
  form.set("file", file);
  return request("/onboarding/parse-resume", { method: "POST", body: form });
}

export async function fetchProfile(): Promise<Profile> {
  return request("/profile");
}

export async function updateProfile(
  fields: Partial<Pick<Profile, "background" | "cvBase" | "jobCriteria">>,
): Promise<Profile> {
  return request("/profile", { method: "PUT", ...json(fields) });
}

export async function generateCv(input: { postingUrl?: string; postingText?: string; postingHint?: string }): Promise<GenerationResult> {
  return request("/cv", { method: "POST", ...json(input) });
}

export async function generateLetter(input: { postingUrl?: string; postingText?: string; postingHint?: string }): Promise<GenerationResult> {
  return request("/letter", { method: "POST", ...json(input) });
}

export async function askQuestion(input: { question: string; postingUrl?: string; postingHint?: string }): Promise<GenerationResult> {
  return request("/answer", { method: "POST", ...json(input) });
}

export async function editThread(threadId: number, message: string): Promise<GenerationResult> {
  return request(`/threads/${threadId}/edit`, { method: "POST", ...json({ message }) });
}

export function cvPdfUrl(threadId: number): string {
  return `${BASE}/threads/${threadId}/pdf`;
}
