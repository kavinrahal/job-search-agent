import { useState, Fragment } from "react";
import { useDiscoveries } from "../hooks/useDashboardData";
import type { DiscoveredPosting } from "../types";
import { InfoTooltip } from "../components/InfoTooltip";

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
  strong_match: "bg-emerald-100 text-emerald-700",
  good_match:   "bg-blue-100 text-blue-700",
  weak_match:   "bg-amber-100 text-amber-700",
  discard:      "bg-red-100 text-red-700",
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
  seek_alert:     "bg-purple-50 text-purple-700",
  linkedin_alert: "bg-sky-50 text-sky-700",
  jora_alert:     "bg-teal-50 text-teal-700",
  greenhouse:     "bg-lime-50 text-lime-700",
  lever:          "bg-orange-50 text-orange-700",
  adzuna:         "bg-rose-50 text-rose-700",
};

function sourceLabel(source: string): string {
  return SOURCE_LABELS[source] ?? source.replace(/_alert$/, "").replace(/\b\w/g, c => c.toUpperCase());
}

function SourceBadge({ source }: { source: string }) {
  if (!source) return null;
  return (
    <span className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-medium ${SOURCE_STYLES[source] ?? "bg-gray-100 text-gray-600"}`}>
      {sourceLabel(source)}
    </span>
  );
}

const MATCH_STYLES: Record<string, string> = {
  strong:     "text-emerald-600",
  good:       "text-blue-600",
  acceptable: "text-amber-600",
  weak:       "text-amber-600",
  excluded:   "text-red-500",
  preferred:  "text-emerald-600",
  acceptable2:"text-blue-600",
  missing:    "text-gray-400",
  target:     "text-emerald-600",
  flagged_low:     "text-red-500",
  flagged_high:    "text-amber-600",
};

function matchStyle(value: string | null): string {
  if (!value) return "text-gray-400";
  return MATCH_STYLES[value] ?? "text-gray-600";
}

// ---------------------------------------------------------------------------
// Recommendation badge
// ---------------------------------------------------------------------------
function RecBadge({ rec }: { rec: string | null }) {
  if (!rec) return null;
  return (
    <span className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-semibold ${REC_STYLES[rec] ?? "bg-gray-100 text-gray-600"}`}>
      {REC_LABELS[rec] ?? rec}
    </span>
  );
}

// ---------------------------------------------------------------------------
// Discovery card
// ---------------------------------------------------------------------------
function DiscoveryCard({ posting }: { posting: DiscoveredPosting }) {
  const [expanded, setExpanded] = useState(false);

  const hasFlags = posting.orangeFlags.length > 0;
  const [primarySkill, ...otherSkills] = posting.skillMatches;
  const hasDetail = posting.skillMatches.length > 0
    || posting.locationDetail
    || posting.salaryDetail;

  return (
    <div className="flex flex-col rounded-xl border border-gray-200 bg-white p-4 shadow-sm">

      {/* Header */}
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <SourceBadge source={posting.source} />
          <p className="mt-1 font-semibold text-gray-800 leading-snug">
            {posting.company || "Unknown company"}
          </p>
          <p className="mt-0.5 text-sm text-gray-500 leading-snug">{posting.title}</p>
        </div>
        <RecBadge rec={posting.recommendation} />
      </div>

      {/* Disqualifier note */}
      {posting.disqualifierHit && (
        <p className="mt-2 text-xs text-red-500">Disqualifier: {posting.disqualifierHit}</p>
      )}

      {/* Key signals */}
      {hasDetail && (
        <dl className="mt-3 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-sm">
          {posting.locationDetail && (
            <>
              <dt className="text-gray-400">Location</dt>
              <dd className={matchStyle(posting.locationMatch)}>
                {posting.locationDetail}
              </dd>
            </>
          )}
          {primarySkill && (
            <>
              <dt className="text-gray-400">{primarySkill.dimension}</dt>
              <dd className={matchStyle(primarySkill.match)}>
                {primarySkill.detail || "not stated"}
              </dd>
            </>
          )}
          {posting.salaryDetail && (
            <>
              <dt className="text-gray-400">Salary</dt>
              <dd className={matchStyle(posting.salaryAssessment)}>
                {posting.salaryDetail}
              </dd>
            </>
          )}
          {!posting.salaryDetail && posting.salaryAssessment === "missing" && (
            <>
              <dt className="text-gray-400">Salary</dt>
              <dd className="text-gray-400">not listed</dd>
            </>
          )}
        </dl>
      )}

      {/* Orange flags */}
      {hasFlags && (
        <div className="mt-2">
          <button
            onClick={() => setExpanded(e => !e)}
            className="text-xs font-medium text-amber-600 hover:text-amber-700"
          >
            {posting.orangeFlags.length} orange flag{posting.orangeFlags.length > 1 ? "s" : ""}
            {expanded ? " ▲" : " ▼"}
          </button>
          {expanded && (
            <ul className="mt-1.5 space-y-0.5">
              {posting.orangeFlags.map((flag, i) => (
                <li key={i} className="text-xs text-amber-700">• {flag}</li>
              ))}
            </ul>
          )}
        </div>
      )}

      {/* Expanded detail */}
      {expanded && (
        <dl className="mt-3 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 border-t border-gray-100 pt-3 text-sm">
          {posting.experienceDetail && (
            <>
              <dt className="text-gray-400">Experience</dt>
              <dd className={matchStyle(posting.experienceMatch)}>
                {posting.experienceDetail}
              </dd>
            </>
          )}
          {otherSkills.map(skill => (
            <Fragment key={skill.dimension}>
              <dt className="text-gray-400">{skill.dimension}</dt>
              <dd className={matchStyle(skill.match)}>
                {skill.detail || "not stated"}
              </dd>
            </Fragment>
          ))}
          {posting.companyAssessment && (
            <>
              <dt className="text-gray-400">Company</dt>
              <dd className={matchStyle(posting.companyAssessment)}>
                {posting.companyAssessment}
              </dd>
            </>
          )}
          {posting.roleTypeMatch && (
            <>
              <dt className="text-gray-400">Role type</dt>
              <dd className={matchStyle(posting.roleTypeMatch)}>
                {posting.roleTypeMatch}
              </dd>
            </>
          )}
        </dl>
      )}

      {/* Rationale */}
      {posting.rationale && (
        <p className={`mt-3 text-sm text-gray-500 leading-relaxed ${!expanded ? "line-clamp-2" : ""}`}>
          {posting.rationale}
        </p>
      )}

      {/* Footer */}
      <div className="mt-auto flex items-center justify-between border-t border-gray-100 pt-3 mt-3">
        <div className="flex gap-3">
          <a
            href={posting.url}
            target="_blank"
            rel="noopener noreferrer"
            className="text-sm font-medium text-blue-600 hover:text-blue-700"
          >
            View posting
          </a>
          {(posting.rationale || hasFlags || posting.experienceDetail) && (
            <button
              onClick={() => setExpanded(e => !e)}
              className="text-sm text-gray-400 hover:text-gray-600"
            >
              {expanded ? "Less" : "More"}
            </button>
          )}
        </div>
        <span className="text-xs text-gray-400">
          {new Date(posting.discoveredAt).toLocaleDateString("en-AU", {
            day: "2-digit", month: "short",
          })}
        </span>
      </div>
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
  const postings = data?.items.filter(p => p.recommendation !== "discard") ?? [];
  const total = data?.total ?? 0;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h2 className="flex items-center text-lg font-semibold text-gray-700">
          Discoveries
          <InfoTooltip text="Postings found automatically from your sources, ranked against your job criteria. Strong/Good match are worth a look; Weak match is a stretch; Discard didn't meet your criteria and is hidden by default." />
        </h2>
        <span className="text-sm text-gray-400">{total} total</span>
      </div>

      {/* Tabs */}
      <div className="flex flex-wrap gap-2">
        {REC_TABS.map(tab => (
          <button
            key={tab.value}
            onClick={() => setActiveTab(tab.value)}
            className={`rounded-full px-3 py-1 text-sm font-medium transition-colors ${
              activeTab === tab.value
                ? "bg-blue-600 text-white"
                : "border border-gray-200 bg-white text-gray-600 hover:bg-gray-50"
            }`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {error && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          {error}
        </div>
      )}

      {loading ? (
        <div className="py-12 text-center text-sm text-gray-400">Loading…</div>
      ) : postings.length === 0 ? (
        <div className="rounded-xl border border-gray-200 bg-white py-12 text-center text-sm text-gray-400 shadow-sm">
          {activeTab === "strong_match"
            ? "No strong matches yet. The agent will notify you when it finds one."
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
