// Pure derivation of a 0-100 match score (and supporting display helpers) from the categorical
// tiers PostingEvaluator already assigns per posting — see JobSearch.Data/PostingEvaluator.cs's
// tool schema for the exhaustive value list per field, and JobSearch.Data/PostingEvaluation.cs
// for the shape. There is no numeric score from the backend; this is a client-side, fully
// explainable composite so the Discover card's meter can show *something* honest without a data
// model change. Kept separate from DiscoveriesPage.tsx so the math is pinned by a plain unit
// test instead of being buried in JSX (same split as resumeSections.ts).

// Every raw tier value the evaluator can emit, across every dimension, mapped to a 0-100 score.
// "missing" (salaryAssessment only) is deliberately absent — an unstated salary isn't a bad
// salary, so it must be skipped rather than scored, see scoreForValue below.
const TIER_SCORES: Record<string, number> = {
  preferred: 100,
  ideal: 100,
  strong: 100,
  target: 100,
  good: 85,
  acceptable: 65,
  weaker: 40,
  flagged_low: 35,
  flagged_high: 35,
  weak: 25,
  excluded: 0,
};

// null for: empty string, "missing", or any value outside the fixed vocabulary above (older rows
// may predate a field). All three are "nothing to score" cases, not a 0 — the caller skips the
// dimension entirely rather than penalizing it.
export function scoreForValue(value: string | null | undefined): number | null {
  if (!value) return null;
  // eslint-disable-next-line security/detect-object-injection -- value is one of the evaluator's own fixed enum values, not arbitrary input
  return TIER_SCORES[value] ?? null;
}

export interface ScorableEvaluation {
  locationMatch: string | null;
  experienceMatch: string | null;
  skillMatches: { match: string }[];
  salaryAssessment: string | null;
  companyAssessment: string | null;
  roleTypeMatch: string | null;
}

// Mean of every present dimension (skill matches averaged into one component first), rounded and
// clamped 0-100. Returns null — not a fake 0/50 — when literally no dimension has a usable value,
// so the caller can hide the meter entirely for fully-legacy rows.
export function computeMatchScore(posting: ScorableEvaluation): number | null {
  const components: number[] = [];

  const location = scoreForValue(posting.locationMatch);
  if (location !== null) components.push(location);

  const experience = scoreForValue(posting.experienceMatch);
  if (experience !== null) components.push(experience);

  const skillScores = posting.skillMatches.map(s => scoreForValue(s.match)).filter((s): s is number => s !== null);
  if (skillScores.length > 0) {
    components.push(skillScores.reduce((sum, s) => sum + s, 0) / skillScores.length);
  }

  const salary = scoreForValue(posting.salaryAssessment);
  if (salary !== null) components.push(salary);

  const company = scoreForValue(posting.companyAssessment);
  if (company !== null) components.push(company);

  const roleType = scoreForValue(posting.roleTypeMatch);
  if (roleType !== null) components.push(roleType);

  if (components.length === 0) return null;

  const mean = components.reduce((sum, c) => sum + c, 0) / components.length;
  return Math.max(0, Math.min(100, Math.round(mean)));
}

// The one-line card summary: the rationale's first sentence, falling back to the whole thing if
// no sentence boundary is found (short/malformed rationale), hard-capped as a safety net against
// an unexpectedly long "sentence".
const SUMMARY_MAX_LENGTH = 140;

export function summarizeRationale(rationale: string | null | undefined): string {
  const text = rationale?.trim();
  if (!text) return "No rationale recorded for this posting.";
  const sentence = text.match(/^[^.!?]*[.!?]/)?.[0]?.trim() || text;
  if (sentence.length <= SUMMARY_MAX_LENGTH) return sentence;
  return `${sentence.slice(0, SUMMARY_MAX_LENGTH - 1).trimEnd()}…`;
}

// Human labels for the fixed set of tier values PostingEvaluator can emit across every
// dimension (locationMatch, experienceMatch, skillMatches[].match, salaryAssessment,
// companyAssessment, roleTypeMatch) — display only, never stored. Falls back to the raw value
// for anything outside that set, same convention as resumeSections.ts's sectionLabel.
const VALUE_LABELS: Record<string, string> = {
  preferred: "Preferred",
  ideal: "Ideal",
  strong: "Strong",
  target: "On target",
  good: "Good",
  acceptable: "Acceptable",
  weaker: "Weaker fit",
  flagged_low: "Below range",
  flagged_high: "Above range",
  weak: "Weak",
  excluded: "Excluded",
  missing: "Not stated",
};

export function dimensionValueLabel(value: string): string {
  // eslint-disable-next-line security/detect-object-injection -- value is one of the evaluator's own fixed enum values, not arbitrary input
  return VALUE_LABELS[value] ?? value;
}

// Coarse bucket for coloring a per-dimension badge in the detail drawer, derived from the same
// score used for the meter so the two stay consistent — a dimension that pulls the composite
// score up reads as "high" wherever it's shown.
export type DimensionLevel = "high" | "medium" | "low" | "none";

export function dimensionLevel(value: string | null | undefined): DimensionLevel {
  const score = scoreForValue(value);
  if (score === null) return "none";
  if (score >= 85) return "high";
  if (score >= 65) return "medium";
  return "low";
}
