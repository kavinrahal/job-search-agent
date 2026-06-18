import type {
  Summary,
  EmailsResponse,
  ApplicationsResponse,
  ApplicationWithEvents,
  ActivityItem,
  DiscoveriesResponse,
  HealthStatus,
} from "./types";

// VITE_API_URL is set in production to the Railway dashboard URL.
// In local dev it's unset and Vite's proxy forwards /api to localhost:5000.
const BASE = (import.meta.env.VITE_API_URL ?? "") + "/api/v1";

async function get<T>(path: string): Promise<T> {
  const res = await fetch(`${BASE}${path}`);
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
  return res.json();
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
  const q = new URLSearchParams();
  if (params.page) q.set("page", String(params.page));
  if (params.pageSize) q.set("pageSize", String(params.pageSize));
  if (params.category) q.set("category", params.category);
  if (params.jobRelatedOnly) q.set("jobRelatedOnly", "true");
  if (params.from) q.set("from", params.from);
  if (params.to) q.set("to", params.to);
  return get(`/emails?${q}`);
}

export async function fetchApplications(params: {
  status?: string;
  page?: number;
  pageSize?: number;
} = {}): Promise<ApplicationsResponse> {
  const q = new URLSearchParams();
  if (params.status) q.set("status", params.status);
  if (params.page) q.set("page", String(params.page));
  if (params.pageSize) q.set("pageSize", String(params.pageSize));
  return get(`/applications?${q}`);
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
  const q = new URLSearchParams();
  if (params.recommendation) q.set("recommendation", params.recommendation);
  if (params.page) q.set("page", String(params.page));
  if (params.pageSize) q.set("pageSize", String(params.pageSize));
  return get(`/discoveries?${q}`);
}

export async function fetchMe(): Promise<{ email: string }> {
  return get("/auth/me");
}

export async function logout(): Promise<void> {
  const res = await fetch(`${BASE}/auth/logout`, { method: "POST" });
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
}
