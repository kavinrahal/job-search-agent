import type { DiscoveredPosting } from "../types";
import type { BadgeVariant } from "../ui";

// Shared match-score computation and tier mapping for a discovered posting. The card's meter, the
// card's tier badge, the filter tabs, and the breakdown modal's label all derive from here, so no
// two of them can disagree.
//
// There is no server-side score field. The score is a priority-weighted average of the
// per-dimension fit tiers the evaluator already stored, each tier mapped to a 0–1 weight. A
// dimension the evaluator recorded as "missing" (the posting is silent on it) counts as a low
// weight — missing information counts against a posting — while only a genuinely unrecognised tier
// string is excluded from the average.

// ---------------------------------------------------------------------------
// Tier mapping — the qualitative tier (Strong/Good/Weak) the filter, card badge, and modal label
// all key off. The score is the source of truth: the tier is derived from it by threshold, so the
// number and the label can never disagree. A posting with no assessable dimensions at all (score
// null) maps to no tier — no badge, and the "held back" treatment wherever one is needed.
// ---------------------------------------------------------------------------
export type Tier = "all" | "strong" | "good" | "weak";

export const TIER_LABEL: Record<Tier, string> = { all: "All", strong: "Strong", good: "Good", weak: "Weak" };
export const TIER_BADGE: Record<Exclude<Tier, "all">, BadgeVariant> = { strong: "strong", good: "good", weak: "weak" };

// The posting's qualitative tier, derived from its match score: ≥75 strong, ≥50 good, else weak.
// Null only when the score itself is null (nothing assessable — see computeMatchScore).
export function tierOf(posting: DiscoveredPosting): Exclude<Tier, "all"> | null {
  const score = computeMatchScore(posting);
  if (score === null) return null;
  if (score >= 75) return "strong";
  if (score >= 50) return "good";
  return "weak";
}

// ---------------------------------------------------------------------------
// Scoring
// ---------------------------------------------------------------------------

// Weight per fit tier, keyed by dimension. Kept explicit per dimension because the tiers differ
// (location has no "excluded", experience has no "good", etc.) — one shared map would invite
// tiers that don't exist for a given field. Number-only; "missing" is handled by tierWeight below,
// not by a null entry here.
const WEIGHT = {
  location: { preferred: 1, acceptable: 0.6, weak: 0.25 },
  experience: { ideal: 1, acceptable: 0.6, excluded: 0 },
  skill: { strong: 1, good: 0.75, acceptable: 0.5, excluded: 0 },
  salary: { target: 1, acceptable: 0.7, flagged_low: 0.35, flagged_high: 0.35 },
  fit: { preferred: 1, acceptable: 0.6, weaker: 0.3, excluded: 0 },
} as const;

// Priority multiplier per dimension, mirroring the evaluator's own stated priority order: skill
// dimensions dominate, company/role-type ("fit") are FYI-only and barely move the number, and
// everything else is baseline.
const PRIORITY = { location: 1, experience: 1, skill: 2, salary: 1, fit: 0.4 } as const;

export interface MatchRow {
  label: string;
  detail: string;
  tier: string;
  // Tier weight (0–1), or null when the tier string is unrecognised and so excluded from the score.
  weight: number | null;
  // Dimension priority multiplier applied in the weighted average.
  priority: number;
}

// The evaluator occasionally emits the literal string "null" for an optional detail field instead
// of omitting it (same gotcha PostingEvaluator.cs's NullIfLiteralNull guards against server-side —
// this covers older rows evaluated before that existed, and is a harmless no-op for everything
// evaluated after). `??`/`.length` checks don't catch a real 4-character string, only a real null.
export function cleanDetail(value: string | null | undefined, fallback: string): string {
  if (!value) return fallback;
  const trimmed = value.trim();
  return trimmed.length === 0 || trimmed.toLowerCase() === "null" ? fallback : trimmed;
}

// "Missing" — the posting is silent on a dimension — counts as a low signal, roughly the weakest
// non-excluded tier (matching location.weak): missing information should pull a posting's score
// down, not be waved through. Applied uniformly to every dimension here, so there's no per-field
// special case. Null is reserved for a genuinely unrecognised tier string, which is excluded.
const MISSING_WEIGHT = 0.25;

function tierWeight(table: Record<string, number>, tier: string): number | null {
  if (tier === "missing") return MISSING_WEIGHT;
  // eslint-disable-next-line security/detect-object-injection -- tier comes from the backend's fixed enum, not user input
  return tier in table ? table[tier] : null;
}

export function buildMatchRows(posting: DiscoveredPosting): MatchRow[] {
  const rows: MatchRow[] = [];

  if (posting.locationMatch)
    rows.push({
      label: "Location",
      detail: cleanDetail(posting.locationDetail, "—"),
      tier: posting.locationMatch,
      weight: tierWeight(WEIGHT.location, posting.locationMatch),
      priority: PRIORITY.location,
    });

  if (posting.experienceMatch)
    rows.push({
      label: "Experience",
      detail: cleanDetail(posting.experienceDetail, "—"),
      tier: posting.experienceMatch,
      weight: tierWeight(WEIGHT.experience, posting.experienceMatch),
      priority: PRIORITY.experience,
    });

  for (const skill of posting.skillMatches)
    rows.push({
      label: skill.dimension,
      detail: cleanDetail(skill.detail, "not stated"),
      tier: skill.match,
      weight: tierWeight(WEIGHT.skill, skill.match),
      priority: PRIORITY.skill,
    });

  if (posting.salaryAssessment)
    rows.push({
      label: "Salary",
      detail: cleanDetail(posting.salaryDetail, "not stated"),
      tier: posting.salaryAssessment,
      weight: tierWeight(WEIGHT.salary, posting.salaryAssessment),
      priority: PRIORITY.salary,
    });

  if (posting.companyAssessment)
    rows.push({
      label: "Company",
      detail: "",
      tier: posting.companyAssessment,
      weight: tierWeight(WEIGHT.fit, posting.companyAssessment),
      priority: PRIORITY.fit,
    });

  if (posting.roleTypeMatch)
    rows.push({
      label: "Role type",
      detail: "",
      tier: posting.roleTypeMatch,
      weight: tierWeight(WEIGHT.fit, posting.roleTypeMatch),
      priority: PRIORITY.fit,
    });

  return rows;
}

// Priority-weighted average of the scoreable rows: Σ(tierWeight × priority) / Σ(priority), summing
// only over rows whose tier weight isn't null (an unrecognised tier is excluded from both sides).
// Returns null when nothing scoreable was assessed.
export function scoreFromRows(rows: MatchRow[]): number | null {
  let weightedSum = 0;
  let prioritySum = 0;
  for (const row of rows) {
    if (row.weight === null) continue;
    weightedSum += row.weight * row.priority;
    prioritySum += row.priority;
  }
  if (prioritySum === 0) return null;
  return Math.round((weightedSum / prioritySum) * 100);
}

// 0–100 match score for a posting, or null when nothing scoreable was assessed.
export function computeMatchScore(posting: DiscoveredPosting): number | null {
  return scoreFromRows(buildMatchRows(posting));
}

// The card's one-line read: the first sentence of the rationale, falling back to the whole
// rationale when there's no sentence boundary, hard-capped as a safety net so a runaway rationale
// can't blow out the card.
const SUMMARY_CAP = 140;

export function matchSummaryLine(rationale: string | null): string {
  if (!rationale) return "No rationale recorded for this posting.";
  const trimmed = rationale.trim();
  if (trimmed.length === 0) return "No rationale recorded for this posting.";
  const sentence = trimmed.match(/^[^.!?]*[.!?]/)?.[0]?.trim() ?? trimmed;
  if (sentence.length <= SUMMARY_CAP) return sentence;
  return sentence.slice(0, SUMMARY_CAP - 1).trimEnd() + "…";
}
