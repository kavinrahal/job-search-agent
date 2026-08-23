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
  return {
    seniorityLevel: b.seniorityLevel,
    candidateCurrentExperience: b.candidateCurrentExperience,
    idealMaxYears: b.idealMaxYears,
    acceptableMinYears: b.acceptableMinYears,
    acceptableMaxYears: b.acceptableMaxYears,
    excludedMinYears: b.excludedMinYears,
  };
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

export const SALARY_SLIDER_MIN = 40_000;
export const SALARY_SLIDER_MAX = 250_000;
export const SALARY_SLIDER_STEP = 10_000;
const DEFAULT_MIN = 80_000;
const DEFAULT_MAX = 150_000;

export interface SalaryRange {
  min: number;
  max: number;
}

// Two explicit endpoints, used directly rather than padded into a synthetic band — the user
// states exactly what they're happy with, so that's exactly what becomes the acceptable-minimum/
// flag-below/target and target-max/flag-above thresholds evaluate_posting.md reads (same
// boundary-sharing pattern used elsewhere in this file: salaryFlagBelow === salaryMin). Numeric
// anchors are AUD-magnitude (this app's default currency) regardless of which currency code gets
// attached — the code follows the Location step's country choice, but the slider's own
// 40k-250k range and step size are not converted per-currency. A non-AUD user gets a
// correctly-labeled but AUD-sized range; the full editor's raw number fields are the correction
// path. Full magnitude conversion is out of scope for this v1 wizard.
export function salaryRangePatch(range: SalaryRange, currency: string): CriteriaPatch {
  return {
    currency,
    salaryMin: String(range.min),
    salaryFlagBelow: String(range.min),
    salaryTargetMin: String(range.min),
    salaryMax: String(range.max),
    salaryFlagAbove: String(range.max),
  };
}

function snapToStep(v: number): number {
  return Math.min(SALARY_SLIDER_MAX, Math.max(SALARY_SLIDER_MIN, Math.round(v / SALARY_SLIDER_STEP) * SALARY_SLIDER_STEP));
}

// Best-effort pre-fill: snaps existing salaryMin/salaryMax to the nearest slider steps, filling
// in a sensible default for whichever end is missing (or both, if nothing's on file yet). Swaps
// the pair if historical data somehow has min > max, so the two sliders never render inverted.
export function nearestSalaryRange(data: Pick<JobCriteriaData, "salaryMin" | "salaryMax">): SalaryRange {
  const hasMin = data.salaryMin.trim() !== "" && !Number.isNaN(Number(data.salaryMin));
  const hasMax = data.salaryMax.trim() !== "" && !Number.isNaN(Number(data.salaryMax));
  const min = hasMin ? snapToStep(Number(data.salaryMin)) : DEFAULT_MIN;
  const max = hasMax ? snapToStep(Number(data.salaryMax)) : DEFAULT_MAX;
  return min <= max ? { min, max } : { min: max, max: min };
}

export function formatSalaryAmount(value: number, currency: string): string {
  return `$${value.toLocaleString()} ${currency}`;
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

// A disqualifier is "wizard-shaped" (safe to represent as one plain input, and safe for the
// wizard to overwrite) only if it has no id/signals/notes — anything richer came from the full
// editor and is never shown in, or clobbered by, these inputs.
export function isSimpleDisqualifier(d: Disqualifier): boolean {
  return !d.id.trim() && !d.signals.trim() && !d.notes.trim();
}

export function disqualifierInputsToObjects(inputs: string[]): Disqualifier[] {
  return inputs.map(s => s.trim()).filter(Boolean)
    .map(description => ({ id: "", description, signals: "", notes: "" }));
}

export function simpleDisqualifierDescriptions(existing: Disqualifier[]): string[] {
  return existing.filter(isSimpleDisqualifier).map(d => d.description);
}

// Replaces only the simple (wizard-owned) disqualifiers with whatever the inputs now say;
// anything richer (added via the full editor) is preserved regardless of input content.
export function applyDisqualifierAnswer(existing: Disqualifier[], inputs: string[]): Disqualifier[] {
  const rich = existing.filter(d => !isSimpleDisqualifier(d));
  return [...rich, ...disqualifierInputsToObjects(inputs)];
}

// ---------------------------------------------------------------------------
// Input sanitization
// ---------------------------------------------------------------------------

// Applied to every free-text wizard field at commit time (Next), not on every keystroke, so
// typing stays smooth and nothing vanishes mid-sentence. This criteria data gets interpolated
// verbatim into evaluate_posting.md's prompt as "the candidate's own criteria" on every posting
// evaluation, and is stored/re-rendered indefinitely afterward — worth keeping clean even though
// the blast radius is inherently low (this app has no cross-user path for this data, so an
// adversarial value can only ever skew the candidate's own evaluations). Strips HTML-tag-like
// content and control characters (React already escapes rendered text; this guards the LLM
// prompt and YAML serialization instead), collapses to a single line (every wizard free-text
// field is single-line now), and caps length so one field can't blow out the evaluation prompt.
const MAX_INPUT_LENGTH = 200;

export function sanitizeCriteriaInput(value: string): string {
  return value
    .replace(/<[^>]*>/g, "")
    .replace(/[\x00-\x1F\x7F]/g, " ")
    .replace(/\s+/g, " ")
    .trim()
    .slice(0, MAX_INPUT_LENGTH);
}
