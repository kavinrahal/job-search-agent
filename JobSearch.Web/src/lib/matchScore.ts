import type { DiscoveredPosting } from "../types";
import type { BadgeVariant } from "../ui";

// Shared match-score computation and tier mapping for a discovered posting. The card's meter, the
// card's tier badge, the filter tabs, and the breakdown modal's label all derive from here, so no
// two of them can disagree.
//
// There is no server-side score field. The score is a priority-weighted average of the
// per-dimension fit tiers the evaluator already stored, each tier mapped to a 0–1 weight. A
// dimension the evaluator recorded as "missing" (silent — no signal either way), or whose tier
// isn't recognised, is excluded from the average entirely rather than counted as zero.

// ---------------------------------------------------------------------------
// Tier mapping — the recommendation tiers the filter, card badge, and modal label all key off.
// "discard" (and any unrecognized/missing recommendation) maps to no tier: it never gets its own
// tab and shows no badge, folding into the "held back" treatment wherever one is still needed.
// ---------------------------------------------------------------------------
export type Tier = "all" | "strong" | "good" | "weak";

export const TIER_LABEL: Record<Tier, string> = { all: "All", strong: "Strong", good: "Good", weak: "Weak" };
export const TIER_BADGE: Record<Exclude<Tier, "all">, BadgeVariant> = { strong: "strong", good: "good", weak: "weak" };

export const REC_TO_TIER: Record<string, Exclude<Tier, "all">> = {
  strong_match: "strong",
  good_match: "good",
  weak_match: "weak",
};

// The tier a posting's recommendation maps to, or null for discard/unrecognized/absent.
export function tierOf(posting: DiscoveredPosting): Exclude<Tier, "all"> | null {
  if (!posting.recommendation) return null;
  return REC_TO_TIER[posting.recommendation] ?? null;
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
  // Tier weight (0–1), or null when the dimension is "missing"/unmapped and so excluded from the score.
  weight: number | null;
  // Dimension priority multiplier applied in the weighted average.
  priority: number;
}

// One place decides that "missing" (and any unrecognised tier) contributes no weight, applied
// uniformly to every dimension — so the "missing is always excluded, never scored as zero" rule is
// visible here rather than an implicit fallthrough at each call site.
function tierWeight(table: Record<string, number>, tier: string): number | null {
  if (tier === "missing") return null;
  // eslint-disable-next-line security/detect-object-injection -- tier comes from the backend's fixed enum, not user input
  return tier in table ? table[tier] : null;
}

export function buildMatchRows(posting: DiscoveredPosting): MatchRow[] {
  const rows: MatchRow[] = [];

  if (posting.locationMatch)
    rows.push({
      label: "Location",
      detail: posting.locationDetail ?? "—",
      tier: posting.locationMatch,
      weight: tierWeight(WEIGHT.location, posting.locationMatch),
      priority: PRIORITY.location,
    });

  if (posting.experienceMatch)
    rows.push({
      label: "Experience",
      detail: posting.experienceDetail ?? "—",
      tier: posting.experienceMatch,
      weight: tierWeight(WEIGHT.experience, posting.experienceMatch),
      priority: PRIORITY.experience,
    });

  for (const skill of posting.skillMatches)
    rows.push({
      label: skill.dimension,
      detail: skill.detail.length > 0 ? skill.detail : "not stated",
      tier: skill.match,
      weight: tierWeight(WEIGHT.skill, skill.match),
      priority: PRIORITY.skill,
    });

  if (posting.salaryAssessment)
    rows.push({
      label: "Salary",
      detail: posting.salaryDetail ?? "not stated",
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
// only over rows whose tier weight isn't null (missing/unmapped excluded from both sides). Returns
// null when nothing scoreable was assessed.
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
