import { useState, useEffect } from "react";
import { useSearchParams } from "react-router-dom";
import { fetchApplications, fetchApplicationEvents } from "../api";
import type { Application, ApplicationWithEvents } from "../types";

const STATUS_COLORS: Record<string, string> = {
  Applied:      "bg-blue-100 text-blue-700",
  Acknowledged: "bg-indigo-100 text-indigo-700",
  Screening:    "bg-purple-100 text-purple-700",
  Interviewing: "bg-amber-100 text-amber-700",
  FinalRound:   "bg-orange-100 text-orange-700",
  Offer:        "bg-emerald-100 text-emerald-700",
  Rejected:     "bg-red-100 text-red-700",
  Ghosted:      "bg-gray-100 text-gray-500",
  Withdrawn:    "bg-gray-100 text-gray-500",
};

const STATUS_TABS = ["All", "Applied", "Acknowledged", "Screening", "Interviewing", "FinalRound", "Offer", "Rejected"];

function StatusBadge({ status }: { status: string }) {
  return (
    <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_COLORS[status] ?? "bg-gray-100 text-gray-600"}`}>
      {status}
    </span>
  );
}

function EventTimeline({ data }: { data: ApplicationWithEvents }) {
  return (
    <div className="mt-3 border-t border-gray-100 pt-3">
      <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-400">Timeline</p>
      <ol className="space-y-2">
        {data.events.map(ev => (
          <li key={ev.id} className="flex gap-3 text-sm">
            <span className="mt-0.5 text-gray-300">•</span>
            <div>
              <span className="font-medium text-gray-700">{ev.summary}</span>
              {ev.fromStatus && ev.toStatus && (
                <span className="ml-2 text-xs text-gray-400">
                  {ev.fromStatus} → {ev.toStatus}
                </span>
              )}
              <p className="text-xs text-gray-400">
                {new Date(ev.occurredAt).toLocaleString("en-AU", {
                  day: "2-digit", month: "short", year: "numeric",
                  hour: "2-digit", minute: "2-digit",
                })}
              </p>
            </div>
          </li>
        ))}
      </ol>
    </div>
  );
}

function ApplicationCard({ app }: { app: Application }) {
  const [expanded, setExpanded] = useState(false);
  const [detail, setDetail] = useState<ApplicationWithEvents | null>(null);
  const [loading, setLoading] = useState(false);

  async function toggle() {
    if (!expanded && !detail) {
      setLoading(true);
      try {
        setDetail(await fetchApplicationEvents(app.id));
      } finally {
        setLoading(false);
      }
    }
    setExpanded(e => !e);
  }

  return (
    <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
      <button
        onClick={toggle}
        className="flex w-full items-start justify-between gap-4 text-left"
      >
        <div className="min-w-0">
          <p className="font-semibold text-gray-800">{app.company}</p>
          <p className="mt-0.5 truncate text-sm text-gray-500">{app.roleTitle || "—"}</p>
        </div>
        <div className="flex shrink-0 flex-col items-end gap-1">
          <StatusBadge status={app.status} />
          <span className="text-xs text-gray-400">
            Updated {new Date(app.updatedAt).toLocaleDateString("en-AU", {
              day: "2-digit", month: "short",
            })}
          </span>
        </div>
      </button>

      {expanded && (
        loading
          ? <p className="mt-3 text-sm text-gray-400">Loading…</p>
          : detail && <EventTimeline data={detail} />
      )}
    </div>
  );
}

export function ApplicationsPage() {
  const [searchParams] = useSearchParams();
  const initialStatus = searchParams.get("status") ?? "All";

  const [apps, setApps] = useState<Application[]>([]);
  const [total, setTotal] = useState(0);
  const [activeTab, setActiveTab] = useState(
    STATUS_TABS.includes(initialStatus) ? initialStatus : "All"
  );
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setLoading(true);
    setError(null);
    fetchApplications({
      status: activeTab === "All" ? undefined : activeTab,
      pageSize: 100,
    })
      .then(res => { setApps(res.items); setTotal(res.total); })
      .catch(e => setError(e instanceof Error ? e.message : "Failed to load"))
      .finally(() => setLoading(false));
  }, [activeTab]);

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold text-gray-700">Applications</h2>
        <span className="text-sm text-gray-400">{total} total</span>
      </div>

      {/* Status tabs */}
      <div className="flex flex-wrap gap-2">
        {STATUS_TABS.map(tab => (
          <button
            key={tab}
            onClick={() => setActiveTab(tab)}
            className={`rounded-full px-3 py-1 text-sm font-medium transition-colors ${
              activeTab === tab
                ? "bg-blue-600 text-white"
                : "border border-gray-200 bg-white text-gray-600 hover:bg-gray-50"
            }`}
          >
            {tab}
          </button>
        ))}
      </div>

      {error && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">{error}</div>
      )}

      {loading ? (
        <div className="py-12 text-center text-sm text-gray-400">Loading…</div>
      ) : apps.length === 0 ? (
        <div className="rounded-xl border border-gray-200 bg-white py-12 text-center text-sm text-gray-400 shadow-sm">
          No applications{activeTab !== "All" ? ` with status "${activeTab}"` : ""} yet.
        </div>
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {apps.map(app => <ApplicationCard key={app.id} app={app} />)}
        </div>
      )}
    </div>
  );
}
