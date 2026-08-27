import { useState, Fragment } from "react";
import { useDiscoveries } from "../hooks/useDashboardData";
import type { DiscoveredPosting } from "../types";
import { GenerationDrawer, type GenerationKind } from "../components/GenerationDrawer";
import {
  Surface,
  Button,
  Badge,
  Callout,
  EmptyState,
  MatchReason,
  SegmentedControl,
  SkeletonList,
  Tooltip,
  SearchIcon,
  type BadgeVariant,
} from "../ui";

// ---------------------------------------------------------------------------
// Recommendation config
// ---------------------------------------------------------------------------
const REC_TABS = [
  { value: "",             label: "All"          },
  { value: "strong_match", label: "Strong Match" },
  { value: "good_match",   label: "Good Match"   },
  { value: "weak_match",   label: "Weak Match"   },
] as const;

const REC_BADGE: Record<string, BadgeVariant> = {
  strong_match: "strong",
  good_match: "good",
  weak_match: "weak",
  discard: "live",
};

const REC_LABELS: Record<string, string> = {
  strong_match: "Strong Match",
  good_match:   "Good Match",
  weak_match:   "Weak Match",
  discard:      "Discard",
};

// Matches the `Source` values JobAlertProcessor/GreenhouseFetcher/LeverFetcher/AdzunaFetcher
// write to DiscoveredPosting (JobSearchAgent/Workers/JobAlertProcessor.cs,
// JobSearchAgent/Integrations/*Fetcher.cs). Falls back to a title-cased, "_alert"-stripped
// version of unrecognized values, so a new fetcher/source doesn't need a frontend change to
// show up reasonably.
const SOURCE_LABELS: Record<string, string> = {
  seek_alert:     "Seek",
  linkedin_alert: "LinkedIn",
  jora_alert:     "Jora",
  greenhouse:     "Greenhouse",
  lever:          "Lever",
  adzuna:         "Adzuna",
};

// source is a backend Source enum value (see the fetcher comment above SOURCE_LABELS), and the
// ?? fallback already covers any value outside the known set — same call across this file's
// other Record lookups (REC_BADGE/REC_LABELS/MATCH_TONE below).
function sourceLabel(source: string): string {
  // eslint-disable-next-line security/detect-object-injection
  return SOURCE_LABELS[source] ?? source.replace(/_alert$/, "").replace(/\b\w/g, c => c.toUpperCase());
}

// Every source gets the same neutral Badge — Badge's five variants are all semantically
// meaningful (strong/good/weak/live/neutral), and a source (Seek vs. Greenhouse) is not itself
// a positive or negative signal, so unlike the six decorative source colours this replaces,
// there is nothing here for a variant to say beyond "this is where it came from".
function SourceBadge({ source }: { source: string }) {
  if (!source) return null;
  return <Badge variant="neutral">{sourceLabel(source)}</Badge>;
}

const MATCH_TONE: Record<string, string> = {
  strong: "text-pos",
  preferred: "text-pos",
  target: "text-pos",
  good: "text-ink-2",
  acceptable2: "text-ink-2",
  acceptable: "text-brass",
  weak: "text-brass",
  flagged_high: "text-brass",
  excluded: "text-ember",
  flagged_low: "text-ember",
  missing: "text-faint",
};

// value is a backend match-tier enum value; ?? fallback covers anything unrecognized.
function matchTone(value: string | null): string {
  if (!value) return "text-faint";
  // eslint-disable-next-line security/detect-object-injection
  return MATCH_TONE[value] ?? "text-muted";
}

// Relative for anything recent enough that "how fresh is this" is the actual question — an
// absolute date reads the same whether it was found an hour ago or three days ago, and the
// whole point of showing this is telling a brand-new posting apart from a stale one at a
// glance. Falls back to an absolute date once relative time stops being the useful framing.
function discoveredLabel(iso: string): string {
  const minutes = Math.floor((Date.now() - new Date(iso).getTime()) / 60_000);
  if (minutes < 1) return "Just now";
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  if (days < 7) return `${days}d ago`;
  return new Date(iso).toLocaleDateString("en-AU", { day: "2-digit", month: "short" });
}

// ---------------------------------------------------------------------------
// Recommendation badge
// ---------------------------------------------------------------------------
// rec is a backend recommendation enum value (strong_match/good_match/weak_match/discard);
// both lookups below fall back gracefully for anything else.
function RecBadge({ rec }: { rec: string | null }) {
  if (!rec) return null;
  // eslint-disable-next-line security/detect-object-injection
  return <Badge variant={REC_BADGE[rec] ?? "neutral"}>{REC_LABELS[rec] ?? rec}</Badge>;
}

// ---------------------------------------------------------------------------
// Discovery card
// ---------------------------------------------------------------------------
function DiscoveryCard({ posting }: { posting: DiscoveredPosting }) {
  const [expanded, setExpanded] = useState(false);
  const [generating, setGenerating] = useState<GenerationKind | null>(null);

  const hasFlags = posting.orangeFlags.length > 0;
  const [primarySkill, ...otherSkills] = posting.skillMatches;
  const hasDetail = posting.skillMatches.length > 0
    || posting.locationDetail
    || posting.salaryDetail;

  return (
    <Surface elevation="raised" className="flex flex-col">

      {/* Header */}
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <SourceBadge source={posting.source} />
          <p className="m-0 mt-1 font-[650] text-ink">
            {posting.company || "Unknown company"}
          </p>
          <p className="m-0 mt-0.5 text-body text-muted">{posting.title}</p>
        </div>
        <RecBadge rec={posting.recommendation} />
      </div>

      {/* Disqualifier note */}
      {posting.disqualifierHit && (
        <p className="m-0 mt-2 text-caption text-ember">Disqualifier: {posting.disqualifierHit}</p>
      )}

      {/* Key signals */}
      {hasDetail && (
        <dl className="m-0 mt-3 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-body">
          {posting.locationDetail && (
            <>
              <dt className="text-faint">Location</dt>
              <dd className={`m-0 ${matchTone(posting.locationMatch)}`}>
                {posting.locationDetail}
              </dd>
            </>
          )}
          {primarySkill && (
            <>
              <dt className="text-faint">{primarySkill.dimension}</dt>
              <dd className={`m-0 ${matchTone(primarySkill.match)}`}>
                {primarySkill.detail || "not stated"}
              </dd>
            </>
          )}
          {posting.salaryDetail && (
            <>
              <dt className="text-faint">Salary</dt>
              <dd className={`m-0 ${matchTone(posting.salaryAssessment)}`}>
                {posting.salaryDetail}
              </dd>
            </>
          )}
          {!posting.salaryDetail && posting.salaryAssessment === "missing" && (
            <>
              <dt className="text-faint">Salary</dt>
              <dd className="m-0 text-faint">not listed</dd>
            </>
          )}
        </dl>
      )}

      {/* Orange flags */}
      {hasFlags && (
        <div className="mt-2">
          <button
            onClick={() => setExpanded(e => !e)}
            className="text-caption font-[650] text-brass hover:opacity-80"
          >
            {posting.orangeFlags.length} orange flag{posting.orangeFlags.length > 1 ? "s" : ""}
            {expanded ? " ▲" : " ▼"}
          </button>
          {expanded && (
            <ul className="m-0 mt-1.5 space-y-0.5 p-0">
              {posting.orangeFlags.map((flag, i) => (
                <li key={i} className="text-caption text-brass">• {flag}</li>
              ))}
            </ul>
          )}
        </div>
      )}

      {/* Expanded detail */}
      {expanded && (
        <dl className="hairline-t m-0 mt-3 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 pt-3 text-body">
          {posting.experienceDetail && (
            <>
              <dt className="text-faint">Experience</dt>
              <dd className={`m-0 ${matchTone(posting.experienceMatch)}`}>
                {posting.experienceDetail}
              </dd>
            </>
          )}
          {otherSkills.map(skill => (
            <Fragment key={skill.dimension}>
              <dt className="text-faint">{skill.dimension}</dt>
              <dd className={`m-0 ${matchTone(skill.match)}`}>
                {skill.detail || "not stated"}
              </dd>
            </Fragment>
          ))}
          {posting.companyAssessment && (
            <>
              <dt className="text-faint">Company</dt>
              <dd className={`m-0 ${matchTone(posting.companyAssessment)}`}>
                {posting.companyAssessment}
              </dd>
            </>
          )}
          {posting.roleTypeMatch && (
            <>
              <dt className="text-faint">Role type</dt>
              <dd className={`m-0 ${matchTone(posting.roleTypeMatch)}`}>
                {posting.roleTypeMatch}
              </dd>
            </>
          )}
        </dl>
      )}

      {/* Rationale */}
      {posting.rationale && (
        <MatchReason
          tone={posting.recommendation === "strong_match" || posting.recommendation === "good_match" ? "why" : "held-back"}
          heading={posting.recommendation === "strong_match" || posting.recommendation === "good_match" ? "Why this one." : "Held back."}
          className={`mt-3 ${!expanded ? "line-clamp-2" : ""}`}
        >
          {posting.rationale}
        </MatchReason>
      )}

      {/* Footer */}
      <div className="hairline-t mt-auto mt-3 pt-3">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <a
              href={posting.url}
              target="_blank"
              rel="noopener noreferrer"
              className="text-body font-[650] text-ember hover:text-ember-hi"
            >
              View posting
            </a>
            {(posting.rationale || hasFlags || posting.experienceDetail) && (
              <button
                onClick={() => setExpanded(e => !e)}
                className="text-body text-faint transition-colors hover:text-muted"
              >
                {expanded ? "Less" : "More"}
              </button>
            )}
          </div>
          <span className="text-caption text-faint" title={new Date(posting.discoveredAt).toLocaleString("en-AU")}>
            Found {discoveredLabel(posting.discoveredAt)}
          </span>
        </div>

        {/* One-tap generation — no posting URL round trip, the backend resolves this
            discovery's own cached posting text (see DiscoveredPosting.PostingText). */}
        <div className="mt-3 flex flex-wrap gap-2">
          <Button size="sm" onClick={() => setGenerating("cv")}>
            Generate CV
          </Button>
          <Button size="sm" variant="subtle" onClick={() => setGenerating("letter")}>
            Cover letter
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
    </Surface>
  );
}

// ---------------------------------------------------------------------------
// Page
// ---------------------------------------------------------------------------
export function DiscoveriesPage() {
  const [activeTab, setActiveTab] = useState<string>("strong_match");

  const { data, error, loading } = useDiscoveries({
    recommendation: activeTab || undefined,
    pageSize: 100,
  });
  // The backend already excludes "discard" whenever no specific recommendation is requested
  // (i.e. the "All" tab) — no client-side filter needed, and no client-side filter wanted:
  // filtering after the server's Skip/Take would silently drop real matches whenever discards
  // dominate the most recent page.
  const postings = data?.items ?? [];
  const total = data?.total ?? 0;

  return (
    <div className="space-y-6">
      <div className="mb-3.5 flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h1 className="m-0 flex items-center text-display font-bold text-ink">
            Discoveries
            <Tooltip text="Postings found automatically from your sources, ranked against your job criteria. Strong/Good match are worth a look; Weak match is a stretch; Discard didn't meet your criteria and is hidden by default." />
          </h1>
          <p className="m-0 text-caption text-faint">Postings we've already found and ranked, so you don't have to go looking.</p>
        </div>
        <span className="text-caption text-faint">{total} total</span>
      </div>

      <SegmentedControl
        label="Filter by recommendation"
        segments={REC_TABS.map(tab => ({ value: tab.value as string, label: tab.label }))}
        value={activeTab}
        onChange={setActiveTab}
      />

      {error && <Callout variant="danger" title={error} />}

      {loading ? (
        <Surface elevation="raised">
          <SkeletonList rows={4} label="Loading discoveries" />
        </Surface>
      ) : postings.length === 0 ? (
        <Surface elevation="raised">
          <EmptyState
            icon={<SearchIcon />}
            title="Nothing here yet"
            body={
              activeTab === ""
                ? "No postings found yet. The agent will notify you when it finds one."
                : activeTab === "strong_match"
                ? "No strong matches yet. The agent will notify you when it finds one."
                // activeTab only ever comes from clicking one of the four hardcoded REC_TABS values.
                // eslint-disable-next-line security/detect-object-injection
                : `No ${REC_LABELS[activeTab] ?? activeTab.replace("_", " ")} postings found.`
            }
          />
        </Surface>
      ) : (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {postings.map(p => <DiscoveryCard key={p.id} posting={p} />)}
        </div>
      )}
    </div>
  );
}
