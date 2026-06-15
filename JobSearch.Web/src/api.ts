import type { Summary, EmailsResponse } from "./types";

const BASE = "/api";

export async function fetchSummary(): Promise<Summary> {
  const res = await fetch(`${BASE}/summary`);
  if (!res.ok) throw new Error(`Failed to fetch summary: ${res.status}`);
  return res.json();
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
  const query = new URLSearchParams();
  if (params.page) query.set("page", String(params.page));
  if (params.pageSize) query.set("pageSize", String(params.pageSize));
  if (params.category) query.set("category", params.category);
  if (params.jobRelatedOnly) query.set("jobRelatedOnly", "true");
  if (params.from) query.set("from", params.from);
  if (params.to) query.set("to", params.to);

  const res = await fetch(`${BASE}/emails?${query}`);
  if (!res.ok) throw new Error(`Failed to fetch emails: ${res.status}`);
  return res.json();
}
