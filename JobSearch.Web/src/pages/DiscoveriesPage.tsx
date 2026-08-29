import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useDiscoveries } from "../hooks/useDashboardData";
import type { DiscoveredPosting } from "../types";
import { GenerationDrawer, type GenerationKind } from "../components/GenerationDrawer";
import { MatchBreakdownModal } from "../components/MatchBreakdownModal";
import { computeMatchScore, matchSummaryLine, tierOf, TIER_LABEL, TIER_BADGE, type Tier } from "../lib/matchScore";
import {
  Badge,
  Button,
  Callout,
  EmptyState,
  IconButton,
  SegmentedControl,
  SkeletonList,
  Surface,
  ExternalLinkIcon,
  SearchIcon,
  cx,
} from "../ui";

// The card meter's fill + percentage colour, keyed off the same tier the badge uses: strong reads
// pos/green, good brass/amber, weak (and a null/discard tier) faint/grey — all existing tokens.
// The tier mapping itself (Tier, TIER_LABEL, TIER_BADGE, tierOf) lives in matchScore.ts so the
// card badge, the filter tabs, and the breakdown modal all agree on one source.
const TIER_METER: Record<Exclude<Tier, "all">, { fill: string; text: string }> = {
  strong: { fill: "bg-pos", text: "text-pos" },
  good: { fill: "bg-brass", text: "text-brass" },
  weak: { fill: "bg-faint", text: "text-faint" },
};

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

// ---------------------------------------------------------------------------
// Discovery card — company/role/badge, a tier-coloured match meter + one-line read, then the
// generate and "Read more" buttons. The full per-dimension breakdown lives behind "Read more"
// (MatchBreakdownModal), so the card itself stays deliberately minimal.
// ---------------------------------------------------------------------------
function DiscoveryCard({ posting, highlighted }: { posting: DiscoveredPosting; highlighted?: boolean }) {
  const [generating, setGenerating] = useState<GenerationKind | null>(null);
  const [breakdownOpen, setBreakdownOpen] = useState(false);
  const tier = tierOf(posting);
  const heldBack = tier === "weak" || tier === null;
  const strong = tier === "strong";
  const score = computeMatchScore(posting);
  const meter = TIER_METER[tier ?? "weak"];

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
            <div className="flex min-w-0 items-center gap-1">
              <p className="m-0 truncate text-body font-[650] tracking-[-.018em] text-ink">
                {posting.company || "Unknown company"}
              </p>
              {posting.url && (
                <IconButton href={posting.url} aria-label="Open posting in a new tab" size="sm" className="flex-none">
                  <ExternalLinkIcon className="h-3.5 w-3.5" />
                </IconButton>
              )}
            </div>
            <p className="m-0 mt-2 text-caption leading-snug text-muted">{posting.title}</p>
          </div>
          {/* eslint-disable-next-line security/detect-object-injection -- tier is Exclude<Tier, "all"> | null, not arbitrary input */}
          {tier && <Badge variant={TIER_BADGE[tier]}>{TIER_LABEL[tier]}</Badge>}
        </div>

        <div className="flex flex-col gap-2.5">
          {score !== null && (
            <div className="flex items-center gap-2.5">
              <div className="surface-sunk h-[3px] flex-1 overflow-hidden rounded-pill">
                <span
                  aria-hidden="true"
                  className={cx(
                    "block h-full w-full origin-left rounded-pill transition-transform duration-500 ease-spring motion-reduce:transition-none",
                    meter.fill,
                  )}
                  style={{ transform: `scaleX(${score / 100})` }}
                />
              </div>
              <span className={cx("text-caption font-[650] tabular-nums leading-none", meter.text)}>{score}%</span>
            </div>
          )}
          <p className="m-0 text-caption leading-snug text-muted">
            <b className="font-[650] text-ink">{heldBack ? "Held back." : "Why this one."}</b>{" "}
            {matchSummaryLine(posting.rationale)}
          </p>
        </div>

        <div className="mt-auto flex flex-col gap-2">
          {strong ? (
            <Button fullWidth cap onClick={() => setGenerating("cv")}>Generate CV</Button>
          ) : (
            <Button variant="ghost" fullWidth onClick={() => setGenerating("cv")}>
              {heldBack ? "Generate anyway" : "Generate CV"}
            </Button>
          )}
          <Button variant="subtle" size="sm" fullWidth onClick={() => setBreakdownOpen(true)}>
            Read more
          </Button>
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

      {breakdownOpen && <MatchBreakdownModal posting={posting} onClose={() => setBreakdownOpen(false)} />}
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
