// One day of the 7-day cumulative trend behind the Today dashboard's stat-block sparklines
// (see GET /api/v1/summary). date is oldest-first, "yyyy-MM-dd".
export interface SummaryHistoryDay {
  date: string;
  applicationsSent: number;
  replyRate: number;
}

export interface Summary {
  applications: {
    total: number;
    byStatus: Record<string, number>;
  };
  history: SummaryHistoryDay[];
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

// Mirrors JobSearch.Data.ItemOverride — shared by ExperienceOverride.achievements and
// ProjectOverride.highlights. order: null keeps the item's natural (Background) position; set
// it to move the item to a specific rendered position (see ResumeRenderer.RenderBulletList's
// two-group sort — an explicit order always wins over natural position).
export interface ItemOverride {
  index: number;
  included: boolean;
  textOverride: string | null;
  order: number | null;
}

// Mirrors JobSearch.Data.ExperienceOverride, referencing Background.experience by positional
// index (not a stored id — see UserResumeData.cs's own comment on why).
export interface ExperienceOverride {
  experienceIndex: number;
  included: boolean;
  companyDescriptionOverride: string | null;
  achievements: ItemOverride[];
  extraAchievements: string[];
  notes: string | null;
}

// Mirrors JobSearch.Data.ProjectOverride, referencing Background.projects by positional index.
export interface ProjectOverride {
  projectIndex: number;
  included: boolean;
  descriptionOverride: string | null;
  highlights: ItemOverride[];
  extraHighlights: string[];
}

// Mirrors JobSearch.Data.SkillsSectionEntry — the actual rendered Skills section, authored
// independently of Background.skills (see UserResume.cs's own comment on why).
export interface SkillsSectionEntry {
  label: string;
  items: string[];
}

// GET/PUT /resume — the curation half of the resume-builder data model (UserResume), distinct
// from the raw-fact Background editor at /profile. The four override fields are always present
// on a GET response (never omitted, unlike the PUT request's optional partial-update fields).
export interface ResumeData {
  summary: string;
  sectionConfig: SectionConfigEntry[];
  experienceOverrides: ExperienceOverride[];
  projectOverrides: ProjectOverride[];
  skillsSection: SkillsSectionEntry[];
  updatedAt: string;
}

// POST /resume/preview's response — the real ResumeRenderer.Render output for an unsaved
// draft, subset-markdown so renderResumeMarkdown.ts can display it without a second
// implementation of ResumeRenderer's merge logic (see that endpoint's own comment).
export interface ResumePreviewResponse {
  markdown: string;
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

