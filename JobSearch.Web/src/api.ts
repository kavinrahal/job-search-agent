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

// VITE_API_URL is set in production to the Railway dashboard URL.
// In local dev it's unset and Vite's proxy forwards /api to localhost:5000.
const BASE = (import.meta.env.VITE_API_URL ?? "") + "/api/v1";

async function get<T>(path: string): Promise<T> {
  const res = await fetch(`${BASE}${path}`);
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
  return res.json();
}

function qs(params: object): string {
  const q = new URLSearchParams();
  for (const [k, v] of Object.entries(params))
    if (v != null && v !== false) q.set(k, String(v));
  const s = q.toString();
  return s ? "?" + s : "";
}

export async function fetchSummary(): Promise<Summary> {
  return get("/summary");
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
  return get(`/emails${qs(params)}`);
}

export async function fetchApplications(params: {
  status?: string;
  page?: number;
  pageSize?: number;
} = {}): Promise<ApplicationsResponse> {
  return get(`/applications${qs(params)}`);
}

export async function fetchApplicationEvents(id: number): Promise<ApplicationWithEvents> {
  return get(`/applications/${id}/events`);
}

export async function fetchActivity(limit = 30): Promise<ActivityItem[]> {
  return get(`/activity?limit=${limit}`);
}

export async function fetchHealth(): Promise<HealthStatus> {
  const res = await fetch(`${BASE}/health`);
  // 503 means stale — still parse the body
  if (!res.ok && res.status !== 503) throw new Error(`${res.status} ${res.statusText}`);
  return res.json();
}

export async function fetchDiscoveries(params: {
  recommendation?: string;
  page?: number;
  pageSize?: number;
} = {}): Promise<DiscoveriesResponse> {
  return get(`/discoveries${qs(params)}`);
}

export async function fetchMe(): Promise<{ email: string; needsOnboarding: boolean }> {
  return get("/auth/me");
}

export async function logout(): Promise<void> {
  const res = await fetch(`${BASE}/auth/logout`, { method: "POST" });
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
}

export async function parseResumeText(text: string): Promise<ParsedResume> {
  const form = new FormData();
  form.set("text", text);
  const res = await fetch(`${BASE}/onboarding/parse-resume`, { method: "POST", body: form });
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
  return res.json();
}

export async function parseResumePdf(file: File): Promise<ParsedResume> {
  const form = new FormData();
  form.set("file", file);
  const res = await fetch(`${BASE}/onboarding/parse-resume`, { method: "POST", body: form });
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
  return res.json();
}

export async function fetchProfile(): Promise<Profile> {
  return get("/profile");
}

export class InsufficientCreditsError extends Error {}

async function postGeneration<T>(path: string, body: object): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (res.status === 402) throw new InsufficientCreditsError("Insufficient credits");
  if (!res.ok) {
    const errBody = await res.json().catch(() => null);
    throw new Error(errBody?.error ?? `${res.status} ${res.statusText}`);
  }
  return res.json();
}

export async function generateCv(input: { postingUrl?: string; postingText?: string }): Promise<GenerationResult> {
  return postGeneration("/cv", input);
}

export async function generateLetter(input: { postingUrl?: string; postingText?: string }): Promise<GenerationResult> {
  return postGeneration("/letter", input);
}

export async function askQuestion(input: { question: string; postingUrl?: string }): Promise<GenerationResult> {
  return postGeneration("/answer", input);
}

export function cvPdfUrl(threadId: number): string {
  return `${BASE}/threads/${threadId}/pdf`;
}

export async function updateProfile(
  fields: Partial<Pick<Profile, "background" | "cvBase" | "jobCriteria">>,
): Promise<Profile> {
  const res = await fetch(`${BASE}/profile`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(fields),
  });
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
  return res.json();
}
