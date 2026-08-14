export interface Summary {
  total: number;
  classified: number;
  jobRelated: number;
  byCategory: Record<string, number>;
  applications: {
    total: number;
    byStatus: Record<string, number>;
  };
}

export interface EmailItem {
  messageId: string;
  from: string;
  subject: string;
  receivedAt: string;
  isJobRelated: boolean;
  category: string | null;
  company: string | null;
  roleTitle: string | null;
  confidence: number | null;
}

export interface EmailsResponse {
  items: EmailItem[];
  total: number;
  page: number;
  pageSize: number;
}

export interface Application {
  id: number;
  company: string;
  roleTitle: string;
  jobUrl: string | null;
  status: string;
  appliedAt: string;
  updatedAt: string;
  notes: string | null;
}

export interface ApplicationsResponse {
  items: Application[];
  total: number;
  page: number;
  pageSize: number;
}

export interface ApplicationEvent {
  id: number;
  eventType: string;
  fromStatus: string | null;
  toStatus: string | null;
  summary: string;
  occurredAt: string;
}

export interface ApplicationWithEvents {
  application: Application;
  events: ApplicationEvent[];
}

export interface ActivityItem {
  applicationId: number;
  company: string;
  roleTitle: string;
  eventType: string;
  fromStatus: string | null;
  toStatus: string | null;
  summary: string;
  occurredAt: string;
}

export interface SkillMatch {
  dimension: string;
  match: string;
  detail: string;
}

export interface DiscoveredPosting {
  id: number;
  url: string;
  source: string;
  title: string;
  company: string;
  recommendation: string | null;
  disqualifierHit: string | null;
  discoveredAt: string;
  evaluatedAt: string | null;
  notificationSent: boolean;
  locationMatch: string | null;
  locationDetail: string | null;
  experienceMatch: string | null;
  experienceDetail: string | null;
  skillMatches: SkillMatch[];
  salaryAssessment: string | null;
  salaryDetail: string | null;
  companyAssessment: string | null;
  roleTypeMatch: string | null;
  orangeFlags: string[];
  rationale: string | null;
}

export interface DiscoveriesResponse {
  items: DiscoveredPosting[];
  total: number;
  page: number;
  pageSize: number;
}

export interface ParsedResume {
  background: string;
  cvBase: string;
}

export interface Profile {
  background: string;
  cvBase: string;
  jobCriteria: string;
  updatedAt: string;
}

export interface HealthStatus {
  status: "ok" | "stale" | "unknown";
  lastRunAt: string | null;
  lastRunAgeMinutes: number | null;
  emailsFetched: number | null;
  emailsClassified: number | null;
  newApplications: number | null;
  durationMs: number | null;
  lastError: string | null;
  totalApplications: number;
  pendingNotifications: number;
}
