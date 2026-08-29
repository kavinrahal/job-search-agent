import type { DiscoveredPosting } from "../types";
import { Modal, Badge, MatchReason, Well, cx, type BadgeVariant } from "../ui";

// The "Read more" breakdown for a Discover card: the same evaluation the agent ran, opened up.
// A derived match score and one-line read at the top, then every dimension the evaluator scored
// with its own detail and fit tier, then any orange flags and the full rationale.
//
// The score is derived here, on the client, from the per-dimension fit tiers the backend already
// stores — there is no separate score field. Each tier maps to a 0–1 weight; the score is their
// average. Dimensions the evaluator left null (not assessed) and salary "missing" (not stated,
// which shouldn't be read as a low score) are left out of the average rather than counted as zero.

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

// Fit tier → badge colour. Top tier reads pos/green, middle brass, anything below faint.
function tierVariant(tier: string): BadgeVariant {
  if (["preferred", "ideal", "strong", "target"].includes(tier)) return "strong";
  if (["acceptable", "good"].includes(tier)) return "good";
  if (tier === "missing") return "neutral";
  return "weak";
}

interface Row {
  label: string;
  detail: string;
  tier: string;
  weight: number | null;
}

function weightOf(table: Record<string, number>, tier: string): number | null {
  // eslint-disable-next-line security/detect-object-injection -- tier comes from the backend's fixed enum, not user input
  return tier in table ? table[tier] : null;
}

function buildRows(posting: DiscoveredPosting): Row[] {
  const rows: Row[] = [];

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

function scoreFrom(rows: Row[]): number | null {
  const weights = rows.map(r => r.weight).filter((w): w is number => w !== null);
  if (weights.length === 0) return null;
  return Math.round((weights.reduce((sum, w) => sum + w, 0) / weights.length) * 100);
}

function band(score: number): { label: string; variant: BadgeVariant } {
  if (score >= 75) return { label: "Strong fit", variant: "strong" };
  if (score >= 50) return { label: "Good fit", variant: "good" };
  return { label: "Weak fit", variant: "weak" };
}

// One-line read: the score band plus how many things the evaluator flagged (excluded/weak
// dimensions and orange flags). Data-grounded, so it can't disagree with the rows below it.
function oneLiner(score: number | null, concerns: number): string {
  const head = score === null ? "Not enough was assessed to score this" : `${score}% match`;
  if (concerns === 0) return `${head} — nothing flagged.`;
  return `${head} — ${concerns} thing${concerns === 1 ? "" : "s"} to check.`;
}

export function MatchBreakdownModal({ posting, onClose }: { posting: DiscoveredPosting; onClose: () => void }) {
  const rows = buildRows(posting);
  const score = scoreFrom(rows);
  const concernRows = rows.filter(r => r.weight !== null && r.weight <= 0.35).length;
  const concerns = concernRows + posting.orangeFlags.length + (posting.disqualifierHit ? 1 : 0);

  return (
    <Modal open onClose={onClose} title={posting.title} description={posting.company || "Unknown company"}>
      <div className="flex flex-col gap-4">
        <Well className="flex items-center justify-between gap-3 px-3.5 py-3">
          <div className="min-w-0">
            <p className="m-0 text-caption text-muted">{oneLiner(score, concerns)}</p>
          </div>
          {score !== null && (
            <div className="flex flex-col items-end gap-1">
              <span className="text-display font-bold tabular-nums leading-none text-ink">{score}</span>
              <Badge variant={band(score).variant}>{band(score).label}</Badge>
            </div>
          )}
        </Well>

        <dl className="m-0 flex flex-col">
          {rows.map((row, i) => (
            <div
              key={`${row.label}-${i}`}
              className={cx("flex items-start justify-between gap-3 py-2.5", i > 0 && "hairline-t")}
            >
              <div className="min-w-0">
                <dt className="m-0 text-caption font-[650] text-ink">{row.label}</dt>
                {row.detail && <dd className="m-0 mt-0.5 text-caption leading-snug text-muted">{row.detail}</dd>}
              </div>
              <Badge variant={tierVariant(row.tier)}>{row.tier.replace(/_/g, " ")}</Badge>
            </div>
          ))}
        </dl>

        {posting.disqualifierHit && (
          <MatchReason tone="held-back" heading="Disqualifier.">
            {posting.disqualifierHit}
          </MatchReason>
        )}

        {posting.orangeFlags.length > 0 && (
          <div>
            <p className="m-0 mb-1.5 text-caption font-[650] text-ink">Orange flags</p>
            <ul className="m-0 flex list-none flex-col gap-1 p-0">
              {posting.orangeFlags.map((flag, i) => (
                <li key={i} className="flex gap-2 text-caption leading-snug text-muted">
                  <span aria-hidden="true" className="text-brass">•</span>
                  <span>{flag}</span>
                </li>
              ))}
            </ul>
          </div>
        )}

        {posting.rationale && (
          <MatchReason heading="Why this one.">{posting.rationale}</MatchReason>
        )}
      </div>
    </Modal>
  );
}
