import type { DiscoveredPosting } from "../types";

// Shared match-score computation for a discovered posting. The card's meter and the breakdown
// modal's number are both derived from here, so the two can never disagree.
//
// There is no server-side score field. The score is the average of the per-dimension fit tiers the
// evaluator already stored, each mapped to a 0–1 weight. Dimensions the evaluator left null (not
// assessed) and salary "missing" (not stated — not a low score) are excluded from the average
// rather than counted as zero.

// Weight per fit tier, keyed by dimension. Kept explicit per dimension because the tiers differ
// (location has no "excluded", experience has no "good", etc.) — one shared map would invite
// tiers that don't exist for a given field.
const WEIGHT = {
  location: { preferred: 1, acceptable: 0.6, weak: 0.25 },
  experience: { ideal: 1, acceptable: 0.6, excluded: 0 },
  skill: { strong: 1, good: 0.75, acceptable: 0.5, excluded: 0 },
  salary: { target: 1, acceptable: 0.7, flagged_low: 0.35, flagged_high: 0.35 },
  fit: { preferred: 1, acceptable: 0.6, weaker: 0.3, excluded: 0 },
} as const;

export interface MatchRow {
  label: string;
  detail: string;
  tier: string;
  weight: number | null;
}

function weightOf(table: Record<string, number>, tier: string): number | null {
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
      weight: weightOf(WEIGHT.location, posting.locationMatch),
    });

  if (posting.experienceMatch)
    rows.push({
      label: "Experience",
      detail: posting.experienceDetail ?? "—",
      tier: posting.experienceMatch,
      weight: weightOf(WEIGHT.experience, posting.experienceMatch),
    });

  for (const skill of posting.skillMatches)
    rows.push({
      label: skill.dimension,
      detail: skill.detail.length > 0 ? skill.detail : "not stated",
      tier: skill.match,
      weight: weightOf(WEIGHT.skill, skill.match),
    });

  if (posting.salaryAssessment)
    rows.push({
      label: "Salary",
      detail: posting.salaryDetail ?? "not stated",
      tier: posting.salaryAssessment,
      // "missing" is not-stated, not a low score — exclude it from the average.
      weight: posting.salaryAssessment === "missing" ? null : weightOf(WEIGHT.salary, posting.salaryAssessment),
    });

  if (posting.companyAssessment)
    rows.push({
      label: "Company",
      detail: "",
      tier: posting.companyAssessment,
      weight: weightOf(WEIGHT.fit, posting.companyAssessment),
    });

  if (posting.roleTypeMatch)
    rows.push({
      label: "Role type",
      detail: "",
      tier: posting.roleTypeMatch,
      weight: weightOf(WEIGHT.fit, posting.roleTypeMatch),
    });

  return rows;
}

export function scoreFromRows(rows: MatchRow[]): number | null {
  const weights = rows.map(r => r.weight).filter((w): w is number => w !== null);
  if (weights.length === 0) return null;
  return Math.round((weights.reduce((sum, w) => sum + w, 0) / weights.length) * 100);
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
