export interface Summary {
  applications: {
    total: number;
    byStatus: Record<string, number>;
  };
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

export const APPLICATION_STATUSES = [
  "Applied", "Acknowledged", "Screening", "Interviewing",
  "FinalRound", "Offer", "Rejected", "Ghosted", "Withdrawn",
] as const;

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

export interface GenerationResult {
  threadId: number;
  text?: string;
  content?: string;
  mode?: "ask_followup" | "final_answer";
  // Claims AccuracyVerifierAgent couldn't trace back to the candidate's own background/CV —
  // absent or empty means nothing was flagged (or, for a follow-up question, nothing final
  // exists yet to check).
  accuracyWarnings?: string[];
}

export interface PostingCandidate {
  title: string;
  company: string;
  location: string;
  url: string;
  source: string;
  postingText: string;
}

export interface Profile {
  background: string;
  cvBase: string;
  jobCriteria: string;
  updatedAt: string;
  hasResumePdf: boolean;
}

// Mirrors JobSearch.Data.SectionConfigEntry — the ordered list of resume sections
// (UserResume.SectionConfigJson). sectionKey is one of ResumeRenderer's known keys
// (experience/education/skills/projects/credentials/publications/volunteering).
export interface SectionConfigEntry {
  sectionKey: string;
  included: boolean;
}

export interface ResumeIndustry {
  key: string;
  displayName: string;
  hasSeniorityToggle: boolean;
}

export interface ResumeTemplatesResponse {
  industries: ResumeIndustry[];
}

// GET/PUT /resume — the curation half of the resume-builder data model (UserResume), distinct
// from the raw-fact Background editor at /profile.
export interface ResumeData {
  summary: string;
  sectionConfig: SectionConfigEntry[];
  updatedAt: string;
}

export interface SourceCatalogItem {
  key: string;
  label: string;
  automatic: boolean;
}

export interface SourcesResponse {
  catalog: SourceCatalogItem[];
  enabled: string[];
  gmailConnected: boolean;
  gmailReadonlyConnected: boolean;
  gmailTrackingMode: "full" | "filter" | "manual" | null;
}

