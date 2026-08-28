import { useState } from "react";
import { useDiscoveries } from "../hooks/useDashboardData";
import type { DiscoveredPosting } from "../types";
import { GenerationDrawer, type GenerationKind } from "../components/GenerationDrawer";
import {
  Badge,
  Button,
  Callout,
  EmptyState,
  MatchReason,
  SegmentedControl,
  SkeletonList,
  Surface,
  SearchIcon,
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
// Discovery card — company/role/badge, a single "why" well, one button. Deliberately minimal:
// no source badge, no key-signals table, no orange-flags expander, no view-posting/more row.
// ---------------------------------------------------------------------------
function DiscoveryCard({ posting }: { posting: DiscoveredPosting }) {
  const [generating, setGenerating] = useState<GenerationKind | null>(null);
  const tier = tierOf(posting);
  const heldBack = tier === "weak" || tier === null;
  const strong = tier === "strong";

  return (
    <Surface padding="md" className="flex h-full flex-col gap-3.5">
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

      <MatchReason tone={heldBack ? "held-back" : "why"} heading={heldBack ? "Held back." : "Why this one."}>
        {posting.rationale ?? "No rationale recorded for this posting."}
      </MatchReason>

      <div className="mt-auto">
        {strong ? (
          <Button fullWidth cap onClick={() => setGenerating("cv")}>Generate CV</Button>
        ) : (
          <Button variant="ghost" fullWidth onClick={() => setGenerating("cv")}>
            {heldBack ? "Generate anyway" : "Generate CV"}
          </Button>
        )}
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
    </Surface>
  );
}

// ---------------------------------------------------------------------------
// Page
// ---------------------------------------------------------------------------
export function DiscoveriesPage() {
  const [activeTab, setActiveTab] = useState<Tier>("all");

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
          {visible.map(p => <DiscoveryCard key={p.id} posting={p} />)}
        </div>
      )}
    </div>
  );
}
