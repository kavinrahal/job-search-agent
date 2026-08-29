import type { DiscoveredPosting } from "../types";
import { buildMatchRows, scoreFromRows, tierOf, TIER_LABEL, TIER_BADGE } from "../lib/matchScore";
import { Modal, Badge, IconButton, MatchReason, Well, ExternalLinkIcon, cx, type BadgeVariant } from "../ui";

// The "Read more" breakdown for a Discover card: the same evaluation the agent ran, opened up.
// A derived match score and one-line read at the top, then every dimension the evaluator scored
// with its own detail and fit tier, then any orange flags and the full rationale.
//
// The score, rows, and the qualitative "fit" label all come from the shared `matchScore` helper —
// the label is the AI's own recommendation tier (not a second verdict re-derived from the score),
// so the modal can never disagree with the card's badge.

// Fit tier → badge colour. Top tier reads pos/green, middle brass, anything below faint.
function tierVariant(tier: string): BadgeVariant {
  if (["preferred", "ideal", "strong", "target"].includes(tier)) return "strong";
  if (["acceptable", "good"].includes(tier)) return "good";
  if (tier === "missing") return "neutral";
  return "weak";
}

// One-line read: the score band plus how many things the evaluator flagged (excluded/weak
// dimensions and orange flags). Data-grounded, so it can't disagree with the rows below it.
function oneLiner(score: number | null, concerns: number): string {
  const head = score === null ? "Not enough was assessed to score this" : `${score}% match`;
  if (concerns === 0) return `${head} — nothing flagged.`;
  return `${head} — ${concerns} thing${concerns === 1 ? "" : "s"} to check.`;
}

export function MatchBreakdownModal({ posting, onClose }: { posting: DiscoveredPosting; onClose: () => void }) {
  const rows = buildMatchRows(posting);
  const score = scoreFromRows(rows);
  const tier = tierOf(posting);
  const concernRows = rows.filter(r => r.weight !== null && r.weight <= 0.35).length;
  const concerns = concernRows + posting.orangeFlags.length + (posting.disqualifierHit ? 1 : 0);

  return (
    <Modal
      open
      onClose={onClose}
      title={posting.title}
      description={posting.company || "Unknown company"}
      titleActions={
        posting.url && (
          <IconButton href={posting.url} aria-label="Open posting in a new tab" size="sm" className="flex-none">
            <ExternalLinkIcon className="h-4 w-4" />
          </IconButton>
        )
      }
    >
      <div className="flex flex-col gap-4">
        <Well className="flex items-center justify-between gap-3 px-3.5 py-3">
          <div className="min-w-0">
            <p className="m-0 text-caption text-muted">{oneLiner(score, concerns)}</p>
          </div>
          {score !== null && (
            <div className="flex flex-col items-end gap-1">
              <span className="text-display font-bold tabular-nums leading-none text-ink">{score}</span>
              {/* eslint-disable-next-line security/detect-object-injection -- tier is Exclude<Tier, "all">, not arbitrary input */}
              {tier && <Badge variant={TIER_BADGE[tier]}>{TIER_LABEL[tier]} fit</Badge>}
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
