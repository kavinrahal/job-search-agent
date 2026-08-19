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

const STATUS_COLORS: Record<string, string> = {
  Applied:      "bg-blue-50 text-blue-700 hover:bg-blue-100 dark:bg-blue-500/15 dark:text-blue-300 dark:hover:bg-blue-500/25",
  Acknowledged: "bg-indigo-50 text-indigo-700 hover:bg-indigo-100 dark:bg-indigo-500/15 dark:text-indigo-300 dark:hover:bg-indigo-500/25",
  Screening:    "bg-purple-50 text-purple-700 hover:bg-purple-100 dark:bg-purple-500/15 dark:text-purple-300 dark:hover:bg-purple-500/25",
  Interviewing: "bg-amber-50 text-amber-700 hover:bg-amber-100 dark:bg-amber-500/15 dark:text-amber-300 dark:hover:bg-amber-500/25",
  FinalRound:   "bg-orange-50 text-orange-700 hover:bg-orange-100 dark:bg-orange-500/15 dark:text-orange-300 dark:hover:bg-orange-500/25",
  Offer:        "bg-emerald-50 text-emerald-700 hover:bg-emerald-100 dark:bg-emerald-500/15 dark:text-emerald-300 dark:hover:bg-emerald-500/25",
  Rejected:     "bg-red-50 text-red-600 hover:bg-red-100 dark:bg-red-500/15 dark:text-red-400 dark:hover:bg-red-500/25",
  Ghosted:      "bg-gray-100 text-gray-500 hover:bg-gray-200 dark:bg-gray-800 dark:text-gray-400 dark:hover:bg-gray-700",
  Withdrawn:    "bg-gray-100 text-gray-500 hover:bg-gray-200 dark:bg-gray-800 dark:text-gray-400 dark:hover:bg-gray-700",
};

const REC_COLORS: Record<string, string> = {
  strong_match: "bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300",
  good_match:   "bg-blue-100 text-blue-700 dark:bg-blue-500/15 dark:text-blue-300",
  weak_match:   "bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300",
  discard:      "bg-red-100 text-red-700 dark:bg-red-500/15 dark:text-red-400",
};

const EVENT_COLORS: Record<string, string> = {
  StatusChanged: "bg-blue-100 text-blue-700 dark:bg-blue-500/15 dark:text-blue-300",
  EmailReceived: "bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-400",
  ManualUpdate:  "bg-purple-100 text-purple-700 dark:bg-purple-500/15 dark:text-purple-300",
};

// Tier2-only — application status counts are the user's own tracked-application records,
// not inbox content, so they're fine to show here (unlike the removed email-category
// breakdown, which was a direct view into inbox content).
function KpiStrip({ summary, onStatusClick }: { summary: Summary; onStatusClick: (status: string) => void }) {
  if (summary.applications.total === 0) return null;

  return (
    <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-800 dark:bg-gray-900">
      <div className="mb-3 flex items-baseline gap-2">
        <p className="text-2xl font-bold text-gray-800 dark:text-white">{summary.applications.total}</p>
        <p className="text-sm text-gray-500 dark:text-gray-400">applications tracked</p>
      </div>
      <div className="flex flex-wrap gap-2">
        {Object.entries(summary.applications.byStatus)
          .sort((a, b) => b[1] - a[1])
          .map(([status, count]) => (
            <button
              key={status}
              onClick={() => onStatusClick(status)}
              className={`rounded-full px-3 py-1 text-sm font-medium transition-colors duration-150 ${STATUS_COLORS[status] ?? "bg-gray-100 text-gray-600 hover:bg-gray-200 dark:bg-gray-800 dark:text-gray-300"}`}
            >
              {status}: {count}
            </button>
          ))}
      </div>
    </div>
  );
}

function RecentApplications() {
  const { data } = useApplications({ pageSize: 5 });
  const items = data?.items ?? [];

  return (
    <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-800 dark:bg-gray-900">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm font-semibold text-gray-700 dark:text-gray-200">Recent applications</p>
        <Link to="/applications" className="text-xs font-medium text-violet-600 hover:underline dark:text-violet-400">View all</Link>
      </div>
      {items.length === 0 ? (
        <p className="text-sm text-gray-400 dark:text-gray-500">No applications logged yet.</p>
      ) : (
        <ul className="space-y-2">
          {items.map(app => (
            <li key={app.id} className="flex items-center justify-between gap-3">
              <div className="min-w-0">
                <p className="truncate text-sm font-medium text-gray-800 dark:text-gray-100">{app.company}</p>
                <p className="truncate text-xs text-gray-400 dark:text-gray-500">{app.roleTitle || "-"}</p>
              </div>
              <span className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_COLORS[app.status] ?? "bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-300"}`}>
                {app.status}
              </span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function RecentDiscoveries() {
  const { data } = useDiscoveries({ pageSize: 5 });
  const items = data?.items ?? [];

  return (
    <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-800 dark:bg-gray-900">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm font-semibold text-gray-700 dark:text-gray-200">Recent discoveries</p>
        <Link to="/discover" className="text-xs font-medium text-violet-600 hover:underline dark:text-violet-400">View all</Link>
      </div>
      {items.length === 0 ? (
        <p className="text-sm text-gray-400 dark:text-gray-500">No postings discovered yet.</p>
      ) : (
        <ul className="space-y-2">
          {items.map(posting => (
            <li key={posting.id} className="flex items-center justify-between gap-3">
              <div className="min-w-0">
                <p className="truncate text-sm font-medium text-gray-800 dark:text-gray-100">{posting.company || "Unknown company"}</p>
                <p className="truncate text-xs text-gray-400 dark:text-gray-500">{posting.title}</p>
              </div>
              {posting.recommendation && (
                <span className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-medium ${REC_COLORS[posting.recommendation] ?? "bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-300"}`}>
                  {posting.recommendation.replace("_", " ")}
                </span>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

// Absorbed from the old standalone Activity page — no separate route/nav item for it
// anymore, this section is where that content lives now.
function ActivityFeed() {
  const [limit, setLimit] = useState(10);
  const { data, loading } = useActivity(limit);
  const items = data ?? [];

  return (
    <div className="rounded-xl border border-gray-200 bg-white shadow-sm dark:border-gray-800 dark:bg-gray-900">
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-gray-100 px-5 py-3 dark:border-gray-800">
        <p className="text-sm font-semibold text-gray-700 dark:text-gray-200">Activity</p>
        <select
          value={limit}
          onChange={e => setLimit(Number(e.target.value))}
          className="rounded-lg border border-gray-200 bg-white px-2 py-1 text-xs text-gray-600 focus:outline-none focus:ring-2 focus:ring-violet-400 dark:border-gray-700 dark:bg-gray-800 dark:text-gray-300 dark:focus:ring-violet-500"
        >
          <option value={10}>Last 10</option>
          <option value={20}>Last 20</option>
          <option value={50}>Last 50</option>
          <option value={100}>Last 100</option>
        </select>
      </div>
      {loading ? (
        <p className="px-5 py-8 text-center text-sm text-gray-400 dark:text-gray-500">Loading…</p>
      ) : items.length === 0 ? (
        <p className="px-5 py-8 text-center text-sm text-gray-400 dark:text-gray-500">No activity yet.</p>
      ) : (
        <ol className="divide-y divide-gray-50 dark:divide-gray-800">
          {items.map((item, i) => (
            <li key={i} className="flex gap-4 px-5 py-4">
              <span className={`mt-0.5 h-fit shrink-0 rounded-full px-2 py-0.5 text-xs font-medium ${EVENT_COLORS[item.eventType] ?? "bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-300"}`}>
                {item.eventType === "StatusChanged" ? "Status" : item.eventType === "EmailReceived" ? "Email" : "Update"}
              </span>
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-baseline justify-between gap-x-2 gap-y-0.5">
                  <p className="min-w-0 truncate font-medium text-gray-800 dark:text-gray-100">
                    {item.company}
                    {item.roleTitle && <span className="ml-1 font-normal text-gray-400 dark:text-gray-500">- {item.roleTitle}</span>}
                  </p>
                  <span className="shrink-0 text-xs text-gray-400 dark:text-gray-500">
                    {new Date(item.occurredAt).toLocaleDateString("en-AU", { day: "2-digit", month: "short", year: "numeric" })}
                  </span>
                </div>
                <p className="mt-0.5 text-sm text-gray-600 dark:text-gray-300">{item.summary}</p>
              </div>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}

export function Tier2Dashboard() {
  const navigate = useNavigate();
  const { data: summary } = useSummary();

  return (
    <div className="space-y-6">
      {summary && <KpiStrip summary={summary} onStatusClick={status => navigate(`/applications?status=${status}`)} />}

      <GeneratePage />

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <RecentApplications />
        <RecentDiscoveries />
      </div>

      <ActivityFeed />
    </div>
  );
}
