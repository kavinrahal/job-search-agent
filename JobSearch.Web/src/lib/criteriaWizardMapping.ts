import type { JobCriteriaData, SkillDimension, Disqualifier } from "./jobCriteriaYaml";

// Pure, framework-free logic behind the onboarding criteria wizard: bucket -> field patches,
// and the reverse (existing field values -> closest bucket, for pre-filling a returning user).
// Kept separate from CriteriaWizard.tsx so the judgment calls encoded here (what "2-4 years"
// actually means in idealMaxYears/acceptableMinYears/etc terms) are pinned by a plain unit test
// instead of being buried in JSX.

export type CriteriaPatch = Partial<JobCriteriaData>;

// ---------------------------------------------------------------------------
// Experience
// ---------------------------------------------------------------------------

export interface ExperienceBucket {
  id: string;
  label: string;
  seniorityLevel: string;
  candidateCurrentExperience: string;
  idealMaxYears: string;
  acceptableMinYears: string;
  acceptableMaxYears: string;
  excludedMinYears: string;
}

// Boundary-sharing pattern (idealMaxYears === acceptableMinYears, acceptableMaxYears ===
// excludedMinYears) is lifted from the owner's own skills/context/job_criteria.yaml, which
// encodes exactly this shape for a ~4-year candidate (ideal.max=4, acceptable.min=4,
// acceptable.max=5, excluded.min=5). "No experience" anchors ideal at 1, not 0 — real entry-level
// postings phrase themselves as "0-1" or "1-2 years", never "0 years required", so anchoring at 0
// would under-match every genuine entry-level posting. The top bucket leaves
// acceptableMaxYears/excludedMinYears open — a 6+-year candidate could have 20, and there's no
// way to judge an upper bound from a single open-ended bucket choice.
export const EXPERIENCE_BUCKETS: ExperienceBucket[] = [
  { id: "none", label: "No experience",
    seniorityLevel: "junior", candidateCurrentExperience: "No professional experience yet",
    idealMaxYears: "1", acceptableMinYears: "1", acceptableMaxYears: "2", excludedMinYears: "2" },
  { id: "1-2", label: "1-2 years",
    seniorityLevel: "junior", candidateCurrentExperience: "1-2 years",
    idealMaxYears: "2", acceptableMinYears: "2", acceptableMaxYears: "3", excludedMinYears: "3" },
  { id: "2-4", label: "2-4 years",
    seniorityLevel: "mid", candidateCurrentExperience: "2-4 years",
    idealMaxYears: "4", acceptableMinYears: "4", acceptableMaxYears: "5", excludedMinYears: "5" },
  { id: "4-6", label: "4-6 years",
    seniorityLevel: "senior", candidateCurrentExperience: "4-6 years",
    idealMaxYears: "6", acceptableMinYears: "6", acceptableMaxYears: "8", excludedMinYears: "8" },
  { id: "6+", label: "6+ years",
    seniorityLevel: "senior", candidateCurrentExperience: "6+ years",
    idealMaxYears: "10", acceptableMinYears: "10", acceptableMaxYears: "", excludedMinYears: "" },
];

export function experienceBucketPatch(id: string): CriteriaPatch {
  const b = EXPERIENCE_BUCKETS.find(x => x.id === id);
  if (!b) return {};
  const { id: _id, label: _label, ...patch } = b;
  return patch;
}

// Best-effort nearest-bucket lookup for pre-filling a returning user (existing data from a prior
// wizard session or the full editor) — not exact, just avoids showing a blank wizard when there's
// already a real answer on file. Compares against acceptableMinYears, the field every bucket sets.
export function nearestExperienceBucket(data: Pick<JobCriteriaData, "acceptableMinYears">): string | null {
  const v = Number(data.acceptableMinYears);
  if (!data.acceptableMinYears.trim() || Number.isNaN(v)) return null;
  return EXPERIENCE_BUCKETS.reduce((best, b) =>
    Math.abs(Number(b.acceptableMinYears) - v) < Math.abs(Number(best.acceptableMinYears) - v) ? b : best
  ).id;
}

// ---------------------------------------------------------------------------
// Salary
// ---------------------------------------------------------------------------

export interface SalaryBucket {
  id: string;
  label: string;
  salaryMin: string;
  salaryTargetMin: string;
  salaryMax: string;
  salaryFlagBelow: string;
  salaryFlagAbove: string;
}

// Same boundary-sharing pattern as Experience (salaryFlagBelow === salaryMin, matching the
// owner's file where flag_below and acceptable_minimum are identical). salaryFlagAbove sits
// roughly 10-15% above the bucket's top, matching the owner's own ratio (target max 140k ->
// flag_above 160k, ~+14%). Numeric anchors are AUD-magnitude (this app's default currency,
// DEFAULTS.currency in jobCriteriaYaml.ts) — the currency *code* attached at save time follows
// the country chosen in the Location step (see CriteriaWizard.tsx), but these bucket sizes are
// not converted per-currency. A non-AUD user gets a correctly-labeled but AUD-sized range; the
// full editor's raw number fields are the correction path. Full magnitude conversion is out of
// scope for this v1 wizard.
export const SALARY_BUCKETS: SalaryBucket[] = [
  { id: "u80", label: "Under 80k",
    salaryMin: "50000", salaryFlagBelow: "50000", salaryTargetMin: "60000", salaryMax: "80000", salaryFlagAbove: "90000" },
  { id: "80-110", label: "80k - 110k",
    salaryMin: "70000", salaryFlagBelow: "70000", salaryTargetMin: "80000", salaryMax: "110000", salaryFlagAbove: "125000" },
  { id: "110-140", label: "110k - 140k",
    salaryMin: "100000", salaryFlagBelow: "100000", salaryTargetMin: "110000", salaryMax: "140000", salaryFlagAbove: "155000" },
  { id: "140-180", label: "140k - 180k",
    salaryMin: "125000", salaryFlagBelow: "125000", salaryTargetMin: "140000", salaryMax: "180000", salaryFlagAbove: "200000" },
  { id: "180+", label: "180k+",
    salaryMin: "160000", salaryFlagBelow: "160000", salaryTargetMin: "180000", salaryMax: "", salaryFlagAbove: "" },
];

export function salaryBucketPatch(id: string, currency: string): CriteriaPatch {
  const b = SALARY_BUCKETS.find(x => x.id === id);
  if (!b) return {};
  const { id: _id, label: _label, ...patch } = b;
  return { ...patch, currency };
}

export function nearestSalaryBucket(data: Pick<JobCriteriaData, "salaryTargetMin">): string | null {
  const v = Number(data.salaryTargetMin);
  if (!data.salaryTargetMin.trim() || Number.isNaN(v)) return null;
  return SALARY_BUCKETS.reduce((best, b) =>
    Math.abs(Number(b.salaryTargetMin) - v) < Math.abs(Number(best.salaryTargetMin) - v) ? b : best
  ).id;
}

// ---------------------------------------------------------------------------
// Skill dimensions
// ---------------------------------------------------------------------------

export interface SkillDimensionAnswer {
  name: string;
  strongMatch: string;
  goodMatch: string;
}

// Writes/replaces index 0 of skillDimensions[] only — the wizard asks one question, not the full
// editor's multi-entry 4-tier table, so it only ever owns the first slot. Any dimensions already
// added via the full editor (index 1+) survive untouched across repeated wizard visits.
export function applySkillDimensionAnswer(existing: SkillDimension[], answer: SkillDimensionAnswer): SkillDimension[] {
  if (!answer.name.trim()) return existing;
  const entry: SkillDimension = {
    name: answer.name, priority: "1",
    strongMatch: answer.strongMatch, goodMatch: answer.goodMatch,
    acceptable: "", excluded: "", notes: "",
  };
  if (existing.length === 0) return [entry];
  return [entry, ...existing.slice(1)];
}

// ---------------------------------------------------------------------------
// Sponsorship
// ---------------------------------------------------------------------------

// Reuses the "silence is not a negative signal" principle and a country-agnostic subset of the
// canonical exclusion phrases verbatim from skills/context/job_criteria.yaml's
// hard_disqualifiers.sponsorship_excluded.signals — deliberately excluding the
// Australia-specific phrasing ("must be an Australian citizen...") from that list, since the
// wizard's Location step means this criteria file could belong to a candidate in any country.
export const SPONSORSHIP_YES_PATCH: CriteriaPatch = {
  sponsorshipModel: "binary",
  sponsorshipDiscardDescription: "Explicitly excludes candidates requiring visa sponsorship",
  sponsorshipDiscardExamples: [
    "no visa sponsorship",
    "unrestricted work rights required",
    "must have full working rights",
    "open to citizens and permanent residents only",
  ].join("\n"),
  sponsorshipInScope: [
    "No mention of work rights or sponsorship (majority of postings — treat as in scope)",
    "Explicit positive mention of sponsorship availability (strong positive signal)",
  ].join("\n"),
  sponsorshipNotes: "Silence is not a negative signal. Apply to anything not explicitly excluding candidates who need sponsorship.",
};

// ---------------------------------------------------------------------------
// Custom disqualifiers
// ---------------------------------------------------------------------------

// A disqualifier is "wizard-shaped" (safe to represent as one plain textarea line, and safe for
// the wizard to overwrite) only if it has no id/signals/notes — anything richer came from the
// full editor and is never shown in, or clobbered by, this textarea.
export function isSimpleDisqualifier(d: Disqualifier): boolean {
  return !d.id.trim() && !d.signals.trim() && !d.notes.trim();
}

export function disqualifierLinesToObjects(text: string): Disqualifier[] {
  return text.split("\n").map(l => l.trim()).filter(Boolean)
    .map(description => ({ id: "", description, signals: "", notes: "" }));
}

export function disqualifiersToTextareaValue(existing: Disqualifier[]): string {
  return existing.filter(isSimpleDisqualifier).map(d => d.description).join("\n");
}

// Replaces only the simple (wizard-owned) disqualifiers with whatever the textarea now says;
// anything richer (added via the full editor) is preserved regardless of textarea content.
export function applyDisqualifierAnswer(existing: Disqualifier[], text: string): Disqualifier[] {
  const rich = existing.filter(d => !isSimpleDisqualifier(d));
  return [...rich, ...disqualifierLinesToObjects(text)];
}
