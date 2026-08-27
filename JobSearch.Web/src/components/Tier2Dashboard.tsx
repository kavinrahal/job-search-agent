import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import {
  useSummary,
  useApplications,
  useDiscoveries,
  useActivity,
} from "../hooks/useDashboardData";
import type { Summary } from "../types";
import { GeneratePage } from "../pages/GeneratePage";
import { Surface, StatBlock, Badge, Ledger, LedgerRow, Select, type BadgeVariant } from "../ui";
import type { StatusTickState } from "../ui";

// Both lookups fall back gracefully for any backend enum value outside the known set — same
// call as ApplicationsPage/DiscoveriesPage's identical status/recommendation lookups.
const STATUS_BADGE: Record<string, BadgeVariant> = {
  Applied: "neutral",
  Acknowledged: "neutral",
  Screening: "good",
  Interviewing: "good",
  FinalRound: "strong",
  Offer: "strong",
  Rejected: "live",
  Ghosted: "weak",
  Withdrawn: "weak",
};

const STATUS_TICK: Record<string, StatusTickState> = {
  Offer: "done",
  Rejected: "pending",
  Ghosted: "pending",
  Withdrawn: "pending",
};

const REC_BADGE: Record<string, BadgeVariant> = {
  strong_match: "strong",
  good_match: "good",
  weak_match: "weak",
  discard: "live",
};

const REC_TICK: Record<string, StatusTickState> = {
  strong_match: "done",
  discard: "pending",
  weak_match: "pending",
};

const EVENT_BADGE: Record<string, BadgeVariant> = {
  StatusChanged: "good",
  EmailReceived: "neutral",
  ManualUpdate: "weak",
};

// Shared between KpiStrip and RecentApplications below. The other lookups (STATUS_TICK/
// REC_BADGE/REC_TICK/EVENT_BADGE) each have exactly one call site, so they're read inline there
// instead — a named wrapper for a lookup used once is one more thing to name, not less code.
// Each keys off a backend enum value (see ApplicationsPage/DiscoveriesPage's matching lookups),
// and the ?? fallback already covers any value outside the known set.
// eslint-disable-next-line security/detect-object-injection
const statusBadge = (status: string): BadgeVariant => STATUS_BADGE[status] ?? "neutral";

// Tier2-only — application status counts are the user's own tracked-application records,
// not inbox content, so they're fine to show here (unlike the removed email-category
// breakdown, which was a direct view into inbox content).
function KpiStrip({ summary, onStatusClick }: { summary: Summary; onStatusClick: (status: string) => void }) {
  if (summary.applications.total === 0) return null;

  return (
    <Surface elevation="raised">
      <StatBlock value={summary.applications.total} label="applications tracked" className="mb-3" />
      <div className="flex flex-wrap gap-2">
        {Object.entries(summary.applications.byStatus)
          .sort((a, b) => b[1] - a[1])
          .map(([status, count]) => (
            <button key={status} type="button" onClick={() => onStatusClick(status)} className="transition-transform active:scale-[.97]">
              <Badge variant={statusBadge(status)}>{status}: {count}</Badge>
            </button>
          ))}
      </div>
    </Surface>
  );
}

function RecentApplications() {
  const { data } = useApplications({ pageSize: 5 });
  const items = data?.items ?? [];

  return (
    <Surface elevation="raised" padding="none" clip>
      <div className="flex flex-wrap items-center justify-between gap-2 px-3.5 pt-3.5 pb-2">
        <p className="m-0 text-body font-[650] text-ink-2">Recent applications</p>
        <Link to="/applications" className="text-caption font-[650] text-ember hover:text-ember-hi">View all</Link>
      </div>
      {items.length === 0 ? (
        <p className="px-3.5 pb-3.5 text-caption text-faint">No applications logged yet.</p>
      ) : (
        <Ledger className="pb-1.5">
          {items.map(app => (
            <LedgerRow
              key={app.id}
              tick={STATUS_TICK[app.status] ?? "live"}
              title={app.company}
              subtitle={app.roleTitle || "-"}
              meta={<Badge variant={statusBadge(app.status)}>{app.status}</Badge>}
            />
          ))}
        </Ledger>
      )}
    </Surface>
  );
}

function RecentDiscoveries() {
  const { data } = useDiscoveries({ pageSize: 5 });
  const items = data?.items ?? [];

  return (
    <Surface elevation="raised" padding="none" clip>
      <div className="flex flex-wrap items-center justify-between gap-2 px-3.5 pt-3.5 pb-2">
        <p className="m-0 text-body font-[650] text-ink-2">Recent discoveries</p>
        <Link to="/discover" className="text-caption font-[650] text-ember hover:text-ember-hi">View all</Link>
      </div>
      {items.length === 0 ? (
        <p className="px-3.5 pb-3.5 text-caption text-faint">No postings discovered yet.</p>
      ) : (
        <Ledger className="pb-1.5">
          {items.map(posting => (
            <LedgerRow
              key={posting.id}
              tick={posting.recommendation ? (REC_TICK[posting.recommendation] ?? "live") : "pending"}
              title={posting.company || "Unknown company"}
              subtitle={posting.title}
              meta={posting.recommendation && (
                <Badge variant={REC_BADGE[posting.recommendation] ?? "neutral"}>
                  {posting.recommendation.replace("_", " ")}
                </Badge>
              )}
            />
          ))}
        </Ledger>
      )}
    </Surface>
  );
}

// Absorbed from the old standalone Activity page — no separate route/nav item for it
// anymore, this section is where that content lives now.
//
// Kept as its own hand-rolled list rather than Ledger/LedgerRow: each entry needs a third line
// (the event summary, e.g. "Application acknowledged"), which LedgerRow has no slot for — its
// title/subtitle pair is full after company/role alone.
function ActivityFeed() {
  const [limit, setLimit] = useState(10);
  const { data, loading } = useActivity(limit);
  const items = data ?? [];

  return (
    <Surface elevation="raised" padding="none" clip>
      <div className="hairline-b flex flex-wrap items-center justify-between gap-2 px-3.5 py-3">
        <p className="m-0 text-body font-[650] text-ink-2">Activity</p>
        <Select
          label="Show"
          className="w-auto"
          value={limit}
          onChange={e => setLimit(Number(e.target.value))}
        >
          <option value={10}>Last 10</option>
          <option value={20}>Last 20</option>
          <option value={50}>Last 50</option>
          <option value={100}>Last 100</option>
        </Select>
      </div>
      {loading ? (
        <p className="px-3.5 py-8 text-center text-caption text-faint">Loading…</p>
      ) : items.length === 0 ? (
        <p className="px-3.5 py-8 text-center text-caption text-faint">No activity yet.</p>
      ) : (
        <ol className="flex flex-col">
          {items.map((item, i) => (
            <li key={i} className={`flex gap-3 px-3.5 py-3 ${i > 0 ? "hairline-t" : ""}`}>
              <span className="mt-0.5 h-fit shrink-0">
                <Badge variant={EVENT_BADGE[item.eventType] ?? "neutral"}>
                  {item.eventType === "StatusChanged" ? "Status" : item.eventType === "EmailReceived" ? "Email" : "Update"}
                </Badge>
              </span>
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-baseline justify-between gap-x-2 gap-y-0.5">
                  <p className="m-0 min-w-0 truncate text-body font-[650] text-ink">
                    {item.company}
                    {item.roleTitle && <span className="ml-1 font-normal text-faint">- {item.roleTitle}</span>}
                  </p>
                  <span className="shrink-0 text-caption text-faint">
                    {new Date(item.occurredAt).toLocaleDateString("en-AU", { day: "2-digit", month: "short", year: "numeric" })}
                  </span>
                </div>
                <p className="m-0 mt-0.5 text-body text-muted">{item.summary}</p>
              </div>
            </li>
          ))}
        </ol>
      )}
    </Surface>
  );
}

export function Tier2Dashboard() {
  const navigate = useNavigate();
  const { data: summary } = useSummary();

  return (
    <div className="space-y-6">
      {summary && <KpiStrip summary={summary} onStatusClick={status => navigate(`/applications?status=${status}`)} />}

      <GeneratePage />

      {/* Third grid column only kicks in at xl (genuinely wide screens) so the fixed-width shell's
          extra room gets put to use rather than just stretching two cards further apart with a
          growing gap between them (per the approved layout prototype). ActivityFeed spans both
          columns at lg so it still reads full-width there, matching its pre-xl position below the
          two-card row today; it only collapses into the third column once there's genuinely a
          third-column's worth of space. */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2 xl:grid-cols-3">
        <RecentApplications />
        <RecentDiscoveries />
        <div className="lg:col-span-2 xl:col-span-1">
          <ActivityFeed />
        </div>
      </div>
    </div>
  );
}
