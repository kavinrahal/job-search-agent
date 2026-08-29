import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useDiscoveries } from "../hooks/useDashboardData";
import type { DiscoveredPosting } from "../types";
import { GenerationDrawer, type GenerationKind } from "../components/GenerationDrawer";
import { computeMatchScore, dimensionLevel, dimensionValueLabel, summarizeRationale, type DimensionLevel } from "../lib/matchScore";
import {
  Badge,
  Button,
  Callout,
  Drawer,
  EmptyState,
  MatchReason,
  SegmentedControl,
  SkeletonList,
  Surface,
  SearchIcon,
  cx,
  styleFor,
  type BadgeVariant,
} from "../ui";

// ---------------------------------------------------------------------------
// Tier config — the three match tiers the filter and card both key off. "discard" (and any
// unrecognized/missing recommendation) never gets its own tab; it folds into the same
// held-back treatment as "weak" wherever a tier still has to be picked for display.
// ---------------------------------------------------------------------------
type Tier = "all" | "strong" | "good" | "weak";

const TIER_LABEL: Record<Tier, string> = { all: "All", strong: "Strong", good: "Good", weak: "Weak" };
const TIER_BADGE: Record<Exclude<Tier, "all">, BadgeVariant> = { strong: "strong", good: "good", weak: "weak" };

const REC_TO_TIER: Record<string, Exclude<Tier, "all">> = {
  strong_match: "strong",
  good_match: "good",
  weak_match: "weak",
};

function tierOf(posting: DiscoveredPosting): Exclude<Tier, "all"> | null {
  if (!posting.recommendation) return null;
  return REC_TO_TIER[posting.recommendation] ?? null;
}

// Meter fill/number colors, keyed by the same recommendation tier as TIER_BADGE (a null tier
// gets the same faint/grey treatment as "weak" — see DiscoveryCard's heldBack). Solid tokens
// (bg-pos, not bg-pos-wash) rather than Badge's wash backgrounds — a wash reads as a label
// background, not a meter fill — but the same three color tokens Badge already uses, nothing new.
const METER_TONE: Record<Exclude<Tier, "all">, { fill: string; text: string }> = {
  strong: { fill: "bg-pos", text: "text-pos" },
  good: { fill: "bg-brass", text: "text-brass" },
  weak: { fill: "bg-faint", text: "text-faint" },
};

// Per-dimension badge coloring in the detail drawer, driven by the same score buckets the
// composite meter uses (see matchScore.ts's dimensionLevel) so a dimension that pulls the score
// up reads as "strong" wherever it appears.
const LEVEL_BADGE: Record<DimensionLevel, BadgeVariant> = {
  high: "strong",
  medium: "good",
  low: "weak",
  none: "neutral",
};

function DimensionBadge({ value }: { value: string }) {
  return <Badge variant={styleFor(LEVEL_BADGE, dimensionLevel(value))}>{dimensionValueLabel(value)}</Badge>;
}

// One row of the detail drawer's breakdown: a label, a tier badge, and optional free-text detail.
function DetailRow({ label, value, detail, divider }: { label: string; value: string; detail?: string | null; divider?: boolean }) {
  return (
    <div className={cx("flex flex-col gap-1 py-2.5", divider && "hairline-t")}>
      <div className="flex items-center justify-between gap-2">
        <span className="text-caption font-[650] text-ink">{label}</span>
        <DimensionBadge value={value} />
      </div>
      {detail && <p className="m-0 text-caption text-muted">{detail}</p>}
    </div>
  );
}

// The "Read more" panel: every structured field the meter and one-liner are derived from, so a
// curious user can see exactly why the score is what it is. Reuses ui/Drawer rather than a new
// modal — same close-button/backdrop-click/Escape/focus-trap contract GenerationDrawer already
// uses on this same card, see this file's own header comment on why.
// Skips any dimension whose raw value is empty (legacy rows), and — per the same "missing isn't
// a bad salary" rule matchScore.ts's scoreForValue follows — an unstated salary too.
function detailRows(posting: DiscoveredPosting): { label: string; value: string; detail?: string | null }[] {
  const rows: { label: string; value: string; detail?: string | null }[] = [];
  if (posting.locationMatch) rows.push({ label: "Location", value: posting.locationMatch, detail: posting.locationDetail });
  if (posting.experienceMatch) rows.push({ label: "Experience", value: posting.experienceMatch, detail: posting.experienceDetail });
  if (posting.salaryAssessment && posting.salaryAssessment !== "missing") {
    rows.push({ label: "Salary", value: posting.salaryAssessment, detail: posting.salaryDetail });
  }
  if (posting.companyAssessment) rows.push({ label: "Company fit", value: posting.companyAssessment });
  if (posting.roleTypeMatch) rows.push({ label: "Role type", value: posting.roleTypeMatch });
  return rows;
}

function DiscoveryDetailDrawer({ posting, tier, onClose }: { posting: DiscoveredPosting; tier: Exclude<Tier, "all"> | null; onClose: () => void }) {
  const heldBack = tier === "weak" || tier === null;

  return (
    <Drawer open onClose={onClose} title={posting.title} description={posting.company}>
      <div className="flex flex-col gap-4">
        {/* eslint-disable-next-line security/detect-object-injection -- tier is Exclude<Tier, "all">, not arbitrary input */}
        {tier && <Badge variant={styleFor(TIER_BADGE, tier)}>{TIER_LABEL[tier]}</Badge>}

        <div className="flex flex-col">
          {detailRows(posting).map((row, i) => (
            <DetailRow key={row.label} label={row.label} value={row.value} detail={row.detail} divider={i > 0} />
          ))}
        </div>

        {posting.skillMatches.length > 0 && (
          <div className="flex flex-col gap-2">
            <p className="m-0 text-eyebrow uppercase text-faint">Skill matches</p>
            <div className="flex flex-col">
              {posting.skillMatches.map((skill, i) => (
                <DetailRow key={`${skill.dimension}-${i}`} label={skill.dimension} value={skill.match} detail={skill.detail} divider={i > 0} />
              ))}
            </div>
          </div>
        )}

        {posting.orangeFlags.length > 0 && (
          <Callout variant="warning" title="Worth a second look">
            {posting.orangeFlags.join(" · ")}
          </Callout>
        )}

        <MatchReason tone={heldBack ? "held-back" : "why"} heading={heldBack ? "Held back." : "Why this one."}>
          {posting.rationale ?? "No rationale recorded for this posting."}
        </MatchReason>
      </div>
    </Drawer>
  );
}

// Stable per-posting DOM id, so a Today "Worth a look" link (?posting=<id>) can scroll straight
// to the right card once it's loaded here.
function discoveryDomId(postingId: number): string {
  return `discovery-${postingId}`;
}

// A real-data stand-in for the prototype's "Checked 6:12am today" — the most recent
// discoveredAt across the current list, since there's no dedicated "last run" field yet.
function freshnessLabel(postings: DiscoveredPosting[]): string | null {
  if (postings.length === 0) return null;
  const latestIso = postings.reduce(
    (max, p) => (new Date(p.discoveredAt) > new Date(max) ? p.discoveredAt : max),
    postings[0].discoveredAt,
  );
  const latest = new Date(latestIso);
  const sameDay = latest.toDateString() === new Date().toDateString();
  if (!sameDay) {
    return `Checked ${latest.toLocaleDateString("en-AU", { day: "2-digit", month: "short" })}`;
  }
  const time = latest
    .toLocaleTimeString("en-AU", { hour: "numeric", minute: "2-digit" })
    .toLowerCase()
    .replace(" ", "");
  return `Checked ${time} today`;
}

// The score-forward meter: a thin fill bar plus the percentage, colored by the card's overall
// tier (not the score itself) so it agrees with the Badge next to it. Hidden entirely by the
// caller when score is null — a legacy row with no scoreable dimensions falls back to just the
// tier badge instead of a fake 0%.
function MatchMeter({ score, tier }: { score: number; tier: Exclude<Tier, "all"> | null }) {
  const tone = styleFor(METER_TONE, tier ?? "weak");
  return (
    <div className="flex items-center gap-2" role="img" aria-label={`Match score ${score}%`}>
      <div className="h-1.5 flex-1 overflow-hidden rounded-pill bg-sunk">
        <span
          className={cx("block h-full rounded-pill transition-[width] duration-500 ease-spring motion-reduce:transition-none", tone.fill)}
          style={{ width: `${score}%` }}
        />
      </div>
      <span className={cx("text-caption font-[650] tabular-nums", tone.text)}>{score}%</span>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Discovery card — company/role/badge, a score meter, a one-line summary, a "Read more" link to
// the full breakdown, one button. Option 3 (score-forward) from the redesign: the full rationale
// prose moved off the card and into the detail drawer, so the card itself no longer reads as a
// wall of text.
// ---------------------------------------------------------------------------
function DiscoveryCard({ posting, highlighted }: { posting: DiscoveredPosting; highlighted?: boolean }) {
  const [generating, setGenerating] = useState<GenerationKind | null>(null);
  const [showDetail, setShowDetail] = useState(false);
  const tier = tierOf(posting);
  const heldBack = tier === "weak" || tier === null;
  const strong = tier === "strong";
  const score = computeMatchScore(posting);

  return (
    <Surface
      padding="md"
      className={cx(
        "h-full transition-shadow duration-500",
        // Brief "you're here" flash for a card linked to from Today's "Worth a look" — cleared
        // by DiscoveriesPage a couple of seconds after landing, not a permanent state.
        highlighted && "ring-2 ring-ember ring-offset-2 ring-offset-shell",
      )}
    >
      {/* Surface's `className` lands on its outer shell div, which has exactly one child (the
          padded core) — a flex/gap there does nothing for spacing *between* these three blocks.
          This wrapper is what the header/well/button actually need to lay out against. */}
      <div className="flex h-full flex-col gap-5">
        <div className="flex items-start justify-between gap-2">
          <div className="min-w-0">
            <p className="m-0 truncate text-body font-[650] tracking-[-.018em] text-ink">
              {posting.company || "Unknown company"}
            </p>
            <p className="m-0 mt-2 text-caption leading-snug text-muted">{posting.title}</p>
          </div>
          {/* eslint-disable-next-line security/detect-object-injection -- tier is Exclude<Tier, "all"> | null, not arbitrary input */}
          {tier && <Badge variant={TIER_BADGE[tier]}>{TIER_LABEL[tier]}</Badge>}
        </div>

        <div className="flex flex-col gap-2">
          {score !== null && <MatchMeter score={score} tier={tier} />}
          <p className="m-0 text-caption text-ink-2">
            <b className="font-[650] text-ink">{heldBack ? "Held back." : "Why this one."}</b> {summarizeRationale(posting.rationale)}
          </p>
          <button
            type="button"
            onClick={() => setShowDetail(true)}
            className="self-start text-caption font-[650] text-ember hover:text-ember-hi"
          >
            Read more
          </button>
        </div>

        <div className="mt-auto">
          {strong ? (
            <Button fullWidth cap onClick={() => setGenerating("cv")}>Generate CV</Button>
          ) : (
            <Button variant="ghost" fullWidth onClick={() => setGenerating("cv")}>
              {heldBack ? "Generate anyway" : "Generate CV"}
            </Button>
          )}
        </div>
      </div>

      {generating && (
        <GenerationDrawer
          discoveryId={posting.id}
          kind={generating}
          title={posting.title}
          company={posting.company}
          onClose={() => setGenerating(null)}
        />
      )}

      {showDetail && <DiscoveryDetailDrawer posting={posting} tier={tier} onClose={() => setShowDetail(false)} />}
    </Surface>
  );
}

// ---------------------------------------------------------------------------
// Page
// ---------------------------------------------------------------------------
export function DiscoveriesPage() {
  const [activeTab, setActiveTab] = useState<Tier>("all");
  const [searchParams] = useSearchParams();
  // Set once on mount, from the ?posting= a Today "Worth a look" row links in with. Not
  // re-derived on every searchParams change — the deep-link only needs to act once per visit.
  const [linkedPostingId] = useState<number | null>(() => {
    const raw = searchParams.get("posting");
    const parsed = raw !== null ? Number(raw) : NaN;
    return Number.isFinite(parsed) ? parsed : null;
  });
  // Whether the deep-link tab pick below has run yet — guards it to exactly once per visit
  // rather than an effect, since it only needs to react to `loading` finishing, not resync on
  // every render (see the comment on that block for why this is done during render, not in
  // useEffect).
  const [deepLinkResolved, setDeepLinkResolved] = useState(false);
  const [highlightedId, setHighlightedId] = useState<number | null>(null);

  // Fetched once, unfiltered. The backend already excludes "discard" whenever no recommendation
  // filter is sent, so every item here is strong/good/weak — safe to slice by tier client-side
  // without either leaking a discard into "All" or risking the old per-tab server refetch
  // silently dropping matches to pagination.
  const { data, error, loading } = useDiscoveries({ pageSize: 100 });
  const postings = data?.items ?? [];

  const counts = {
    all: postings.length,
    strong: postings.filter(p => tierOf(p) === "strong").length,
    good: postings.filter(p => tierOf(p) === "good").length,
    weak: postings.filter(p => tierOf(p) === "weak").length,
  };

  const visible = activeTab === "all" ? postings : postings.filter(p => tierOf(p) === activeTab);
  const freshness = freshnessLabel(postings);

  // Deep-link from Today's "Worth a look": once discoveries have loaded, pick the tab that
  // actually shows the linked posting (a null/discard tier isn't shown by any single-tier tab,
  // so this falls back to "all"). Adjusted directly during render rather than in a useEffect —
  // React's documented pattern for "adjust state once new data arrives" — and gated by
  // deepLinkResolved so it only ever fires once, not on every render. Silently resolves with no
  // tab change if the id doesn't match anything currently in the list (e.g. already discarded).
  if (!loading && !deepLinkResolved && linkedPostingId !== null) {
    const target = postings.find(p => p.id === linkedPostingId);
    if (target) {
      const desiredTab: Tier = tierOf(target) ?? "all";
      if (activeTab !== desiredTab) setActiveTab(desiredTab);
    }
    setDeepLinkResolved(true);
  }

  // Once the deep-link's tab pick above has landed, the linked card is actually in the DOM —
  // scroll to it and briefly highlight it. No-ops if the id never matched anything. This is a
  // genuine "synchronize with an external system" effect (DOM scroll position, a timer), not
  // state derivable during render like the tab pick above — same legitimate case
  // useDebouncedPreview's own effect documents.
  useEffect(() => {
    if (!deepLinkResolved || linkedPostingId === null) return;
    const el = document.getElementById(discoveryDomId(linkedPostingId));
    if (!el) return;
    el.scrollIntoView({ behavior: "smooth", block: "center" });
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setHighlightedId(linkedPostingId);
    const timer = setTimeout(() => setHighlightedId(null), 2000);
    return () => clearTimeout(timer);
  }, [deepLinkResolved, activeTab, linkedPostingId]);

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <SegmentedControl
          label="Filter discoveries"
          segments={[
            { value: "all", label: "All", count: counts.all },
            { value: "strong", label: "Strong", count: counts.strong },
            { value: "good", label: "Good", count: counts.good },
            { value: "weak", label: "Weak", count: counts.weak },
          ]}
          value={activeTab}
          onChange={setActiveTab}
        />
        {freshness && <span className="text-meta whitespace-nowrap text-faint">{freshness}</span>}
      </div>

      {error && <Callout variant="danger" title={error} />}

      {loading ? (
        <Surface elevation="raised">
          <SkeletonList rows={4} label="Loading discoveries" />
        </Surface>
      ) : visible.length === 0 ? (
        <Surface elevation="raised">
          <EmptyState
            icon={<SearchIcon />}
            title="Nothing here yet"
            body={
              activeTab === "all"
                ? "No postings found yet. The agent will notify you when it finds one."
                // eslint-disable-next-line security/detect-object-injection -- activeTab is the Tier union, not arbitrary input
                : `No ${TIER_LABEL[activeTab]} matches right now.`
            }
          />
        </Surface>
      ) : (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {visible.map(p => (
            <div key={p.id} id={discoveryDomId(p.id)}>
              <DiscoveryCard posting={p} highlighted={p.id === highlightedId} />
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
