import { useState, Fragment } from "react";
import { useDiscoveries } from "../hooks/useDashboardData";
import type { DiscoveredPosting } from "../types";
import { InfoTooltip } from "../components/InfoTooltip";
import { PageTagline } from "../components/PageTagline";
import { GenerationDrawer, type GenerationKind } from "../components/GenerationDrawer";
import { PRIMARY_BUTTON_SM, SECONDARY_BUTTON } from "../lib/styles";

// ---------------------------------------------------------------------------
// Recommendation config
// ---------------------------------------------------------------------------
const REC_TABS = [
  { value: "",             label: "All"          },
  { value: "strong_match", label: "Strong Match" },
  { value: "good_match",   label: "Good Match"   },
  { value: "weak_match",   label: "Weak Match"   },
] as const;

const REC_STYLES: Record<string, string> = {
  strong_match: "bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300",
  good_match:   "bg-blue-100 text-blue-700 dark:bg-blue-500/15 dark:text-blue-300",
  weak_match:   "bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300",
  discard:      "bg-red-100 text-red-700 dark:bg-red-500/15 dark:text-red-400",
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

const SOURCE_STYLES: Record<string, string> = {
  seek_alert:     "bg-purple-50 text-purple-700 dark:bg-purple-500/10 dark:text-purple-300",
  linkedin_alert: "bg-sky-50 text-sky-700 dark:bg-sky-500/10 dark:text-sky-300",
  jora_alert:     "bg-teal-50 text-teal-700 dark:bg-teal-500/10 dark:text-teal-300",
  greenhouse:     "bg-lime-50 text-lime-700 dark:bg-lime-500/10 dark:text-lime-300",
  lever:          "bg-orange-50 text-orange-700 dark:bg-orange-500/10 dark:text-orange-300",
  adzuna:         "bg-rose-50 text-rose-700 dark:bg-rose-500/10 dark:text-rose-300",
};

// source is a backend Source enum value (see the fetcher comment above SOURCE_LABELS), and the
// ?? fallback already covers any value outside the known set — same call across this file's
// other Record lookups (REC_STYLES/REC_LABELS/MATCH_STYLES below).
function sourceLabel(source: string): string {
  // eslint-disable-next-line security/detect-object-injection
  return SOURCE_LABELS[source] ?? source.replace(/_alert$/, "").replace(/\b\w/g, c => c.toUpperCase());
}

function SourceBadge({ source }: { source: string }) {
  if (!source) return null;
  return (
    // eslint-disable-next-line security/detect-object-injection
    <span className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-medium ${SOURCE_STYLES[source] ?? "bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-300"}`}>
      {sourceLabel(source)}
    </span>
  );
}

const MATCH_STYLES: Record<string, string> = {
  strong:     "text-emerald-600 dark:text-emerald-400",
  good:       "text-blue-600 dark:text-blue-400",
  acceptable: "text-amber-600 dark:text-amber-400",
  weak:       "text-amber-600 dark:text-amber-400",
  excluded:   "text-red-500 dark:text-red-400",
  preferred:  "text-emerald-600 dark:text-emerald-400",
  acceptable2:"text-blue-600 dark:text-blue-400",
  missing:    "text-gray-400 dark:text-gray-500",
  target:     "text-emerald-600 dark:text-emerald-400",
  flagged_low:     "text-red-500 dark:text-red-400",
  flagged_high:    "text-amber-600 dark:text-amber-400",
};

// value is a backend match-tier enum value; ?? fallback covers anything unrecognized.
function matchStyle(value: string | null): string {
  if (!value) return "text-gray-400 dark:text-gray-500";
  // eslint-disable-next-line security/detect-object-injection
  return MATCH_STYLES[value] ?? "text-gray-600 dark:text-gray-400";
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
  return (
    // eslint-disable-next-line security/detect-object-injection
    <span className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-semibold ${REC_STYLES[rec] ?? "bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-300"}`}>
      {/* eslint-disable-next-line security/detect-object-injection */}
      {REC_LABELS[rec] ?? rec}
    </span>
  );
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
    <div className="flex flex-col rounded-xl border border-gray-200 bg-white p-4 shadow-sm transition-shadow duration-150 hover:shadow-md dark:border-gray-800 dark:bg-gray-900 dark:hover:border-gray-700 dark:hover:shadow-none">

      {/* Header */}
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <SourceBadge source={posting.source} />
          <p className="mt-1 font-semibold leading-snug text-gray-800 dark:text-gray-100">
            {posting.company || "Unknown company"}
          </p>
          <p className="mt-0.5 text-sm leading-snug text-gray-500 dark:text-gray-400">{posting.title}</p>
        </div>
        <RecBadge rec={posting.recommendation} />
      </div>

      {/* Disqualifier note */}
      {posting.disqualifierHit && (
        <p className="mt-2 text-xs text-red-500 dark:text-red-400">Disqualifier: {posting.disqualifierHit}</p>
      )}

      {/* Key signals */}
      {hasDetail && (
        <dl className="mt-3 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-sm">
          {posting.locationDetail && (
            <>
              <dt className="text-gray-400 dark:text-gray-500">Location</dt>
              <dd className={matchStyle(posting.locationMatch)}>
                {posting.locationDetail}
              </dd>
            </>
          )}
          {primarySkill && (
            <>
              <dt className="text-gray-400 dark:text-gray-500">{primarySkill.dimension}</dt>
              <dd className={matchStyle(primarySkill.match)}>
                {primarySkill.detail || "not stated"}
              </dd>
            </>
          )}
          {posting.salaryDetail && (
            <>
              <dt className="text-gray-400 dark:text-gray-500">Salary</dt>
              <dd className={matchStyle(posting.salaryAssessment)}>
                {posting.salaryDetail}
              </dd>
            </>
          )}
          {!posting.salaryDetail && posting.salaryAssessment === "missing" && (
            <>
              <dt className="text-gray-400 dark:text-gray-500">Salary</dt>
              <dd className="text-gray-400 dark:text-gray-500">not listed</dd>
            </>
          )}
        </dl>
      )}

      {/* Orange flags */}
      {hasFlags && (
        <div className="mt-2">
          <button
            onClick={() => setExpanded(e => !e)}
            className="text-xs font-medium text-amber-600 transition-colors hover:text-amber-700 dark:text-amber-400 dark:hover:text-amber-300"
          >
            {posting.orangeFlags.length} orange flag{posting.orangeFlags.length > 1 ? "s" : ""}
            {expanded ? " ▲" : " ▼"}
          </button>
          {expanded && (
            <ul className="mt-1.5 space-y-0.5">
              {posting.orangeFlags.map((flag, i) => (
                <li key={i} className="text-xs text-amber-700 dark:text-amber-400">• {flag}</li>
              ))}
            </ul>
          )}
        </div>
      )}

      {/* Expanded detail */}
      {expanded && (
        <dl className="mt-3 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 border-t border-gray-100 pt-3 text-sm dark:border-gray-800">
          {posting.experienceDetail && (
            <>
              <dt className="text-gray-400 dark:text-gray-500">Experience</dt>
              <dd className={matchStyle(posting.experienceMatch)}>
                {posting.experienceDetail}
              </dd>
            </>
          )}
          {otherSkills.map(skill => (
            <Fragment key={skill.dimension}>
              <dt className="text-gray-400 dark:text-gray-500">{skill.dimension}</dt>
              <dd className={matchStyle(skill.match)}>
                {skill.detail || "not stated"}
              </dd>
            </Fragment>
          ))}
          {posting.companyAssessment && (
            <>
              <dt className="text-gray-400 dark:text-gray-500">Company</dt>
              <dd className={matchStyle(posting.companyAssessment)}>
                {posting.companyAssessment}
              </dd>
            </>
          )}
          {posting.roleTypeMatch && (
            <>
              <dt className="text-gray-400 dark:text-gray-500">Role type</dt>
              <dd className={matchStyle(posting.roleTypeMatch)}>
                {posting.roleTypeMatch}
              </dd>
            </>
          )}
        </dl>
      )}

      {/* Rationale */}
      {posting.rationale && (
        <p className={`mt-3 text-sm leading-relaxed text-gray-500 dark:text-gray-400 ${!expanded ? "line-clamp-2" : ""}`}>
          {posting.rationale}
        </p>
      )}

      {/* Footer */}
      <div className="mt-auto mt-3 border-t border-gray-100 pt-3 dark:border-gray-800">
        <div className="flex items-center justify-between">
          <div className="flex gap-3">
            <a
              href={posting.url}
              target="_blank"
              rel="noopener noreferrer"
              className="text-sm font-medium text-violet-600 transition-colors hover:text-violet-700 dark:text-violet-400 dark:hover:text-violet-300"
            >
              View posting
            </a>
            {(posting.rationale || hasFlags || posting.experienceDetail) && (
              <button
                onClick={() => setExpanded(e => !e)}
                className="text-sm text-gray-400 transition-colors hover:text-gray-600 dark:text-gray-500 dark:hover:text-gray-300"
              >
                {expanded ? "Less" : "More"}
              </button>
            )}
          </div>
          <span className="text-xs text-gray-400 dark:text-gray-500" title={new Date(posting.discoveredAt).toLocaleString("en-AU")}>
            Found {discoveredLabel(posting.discoveredAt)}
          </span>
        </div>

        {/* One-tap generation — no posting URL round trip, the backend resolves this
            discovery's own cached posting text (see DiscoveredPosting.PostingText). */}
        <div className="mt-3 flex flex-wrap gap-2">
          <button onClick={() => setGenerating("cv")} className={PRIMARY_BUTTON_SM}>
            Generate CV
          </button>
          <button onClick={() => setGenerating("letter")} className={SECONDARY_BUTTON}>
            Cover letter
          </button>
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
    </div>
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
      <div className="flex items-center justify-between">
        <h2 className="flex items-center text-lg font-semibold text-gray-700 dark:text-gray-200">
          Discoveries
          <InfoTooltip text="Postings found automatically from your sources, ranked against your job criteria. Strong/Good match are worth a look; Weak match is a stretch; Discard didn't meet your criteria and is hidden by default." />
        </h2>
        <span className="text-sm text-gray-400 dark:text-gray-500">{total} total</span>
      </div>
      <PageTagline>Postings we've already found and ranked, so you don't have to go looking.</PageTagline>

      {/* Tabs */}
      <div className="flex flex-wrap gap-2">
        {REC_TABS.map(tab => (
          <button
            key={tab.value}
            onClick={() => setActiveTab(tab.value)}
            className={`rounded-full px-3 py-1 text-sm font-medium transition-colors duration-150 ${
              activeTab === tab.value
                ? "bg-gradient-to-r from-violet-600 to-fuchsia-500 text-white shadow-sm shadow-violet-600/20"
                : "border border-gray-200 bg-white text-gray-600 hover:bg-gray-50 dark:border-gray-700 dark:bg-gray-900 dark:text-gray-300 dark:hover:bg-gray-800"
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {error && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-950/30 dark:text-red-400">
          {error}
        </div>
      )}

      {loading ? (
        <div className="py-12 text-center text-sm text-gray-400 dark:text-gray-500">Loading…</div>
      ) : postings.length === 0 ? (
        <div className="rounded-xl border border-gray-200 bg-white py-12 text-center text-sm text-gray-400 shadow-sm dark:border-gray-800 dark:bg-gray-900 dark:text-gray-500">
          {activeTab === ""
            ? "No postings found yet. The agent will notify you when it finds one."
            : activeTab === "strong_match"
            ? "No strong matches yet. The agent will notify you when it finds one."
            // activeTab only ever comes from clicking one of the four hardcoded REC_TABS values.
            // eslint-disable-next-line security/detect-object-injection
            : `No ${REC_LABELS[activeTab] ?? activeTab.replace("_", " ")} postings found.`}
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {postings.map(p => <DiscoveryCard key={p.id} posting={p} />)}
        </div>
      )}
    </div>
  );
}
