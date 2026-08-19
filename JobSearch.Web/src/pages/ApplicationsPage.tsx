import { useState, type FormEvent } from "react";
import { useSearchParams } from "react-router-dom";
import {
  useApplications,
  useApplicationEvents,
  useCreateApplication,
  useUpdateApplicationStatus,
} from "../hooks/useDashboardData";
import { APPLICATION_STATUSES, type Application, type ApplicationWithEvents } from "../types";
import { InfoTooltip } from "../components/InfoTooltip";
import { PageTagline } from "../components/PageTagline";

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

// Fallback for anything automatic tracking misses — a plain <select> styled to still read as
// a colored status pill. Stops propagation so picking a status doesn't also toggle the card.
function StatusSelect({ status, onChange }: { status: string; onChange: (next: string) => void }) {
  return (
    <select
      value={status}
      onClick={e => e.stopPropagation()}
      onChange={e => onChange(e.target.value)}
      className={`rounded-full border-0 px-2 py-0.5 text-xs font-medium ${STATUS_COLORS[status] ?? "bg-gray-100 text-gray-600"}`}
    >
      {APPLICATION_STATUSES.map(s => <option key={s} value={s}>{s}</option>)}
    </select>
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

function ApplicationCard({ app, onStatusChanged }: { app: Application; onStatusChanged: () => void }) {
  const [expanded, setExpanded] = useState(false);
  const [detail, setDetail] = useState<ApplicationWithEvents | null>(null);
  const { execute, loading } = useApplicationEvents();
  const { execute: updateStatus } = useUpdateApplicationStatus();

  async function toggle() {
    if (!expanded && !detail) {
      setDetail(await execute(app.id));
    }
    setExpanded(e => !e);
  }

  async function handleStatusChange(next: string) {
    if (next === app.status) return;
    await updateStatus(app.id, next);
    setDetail(null); // stale timeline — refetched next time the card expands
    onStatusChanged();
  }

  return (
    <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
      <button
        onClick={toggle}
        className="flex w-full items-start justify-between gap-4 text-left"
      >
        <div className="min-w-0">
          <p className="font-semibold text-gray-800">{app.company}</p>
          <p className="mt-0.5 truncate text-sm text-gray-500">{app.roleTitle || "-"}</p>
        </div>
        <div className="flex shrink-0 flex-col items-end gap-1">
          <StatusSelect status={app.status} onChange={handleStatusChange} />
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

function LogApplicationForm({ onDone }: { onDone: () => void }) {
  const { execute, loading, error } = useCreateApplication();
  const [company, setCompany] = useState("");
  const [roleTitle, setRoleTitle] = useState("");
  const [jobUrl, setJobUrl] = useState("");
  const [companyDomain, setCompanyDomain] = useState("");

  // Rough guess only — strip legal suffixes/punctuation, not a real lookup. The user must
  // confirm or correct it; it's never trusted as-is.
  function guessDomain(name: string) {
    const slug = name
      .toLowerCase()
      .replace(/\b(inc|llc|corp|corporation|ltd|pty|co)\b\.?/g, "")
      .replace(/[^a-z0-9]/g, "");
    return slug ? `${slug}.com` : "";
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!company.trim() || !roleTitle.trim()) return;
    await execute({
      company: company.trim(),
      roleTitle: roleTitle.trim(),
      jobUrl: jobUrl.trim() || undefined,
      companyDomain: companyDomain.trim() || undefined,
    });
    onDone();
  }

  return (
    <form onSubmit={handleSubmit} className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <div>
          <label className="mb-1 block text-xs font-medium text-gray-500">Company</label>
          <input
            value={company}
            onChange={e => {
              setCompany(e.target.value);
              if (!companyDomain) setCompanyDomain(guessDomain(e.target.value));
            }}
            required
            className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm"
          />
        </div>
        <div>
          <label className="mb-1 block text-xs font-medium text-gray-500">Role title</label>
          <input
            value={roleTitle}
            onChange={e => setRoleTitle(e.target.value)}
            required
            className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm"
          />
        </div>
        <div>
          <label className="mb-1 block text-xs font-medium text-gray-500">Job URL (optional)</label>
          <input
            value={jobUrl}
            onChange={e => setJobUrl(e.target.value)}
            className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm"
          />
        </div>
        <div>
          <label className="mb-1 block text-xs font-medium text-gray-500">
            Company email domain (optional)
          </label>
          <input
            value={companyDomain}
            onChange={e => setCompanyDomain(e.target.value)}
            placeholder="acmecorp.com"
            className="w-full rounded-lg border border-gray-200 px-3 py-2 text-sm"
          />
          <p className="mt-1 text-xs text-gray-400">
            Only used if you're on filter-only tracking. Installs a Gmail filter forwarding
            mail from this domain. A rough guess, not verified, check it's right.
          </p>
        </div>
      </div>
      {error && <p className="mt-2 text-sm text-red-700">{error}</p>}
      <div className="mt-3 flex items-center gap-3">
        <button
          type="submit"
          disabled={loading}
          className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:bg-gray-300"
        >
          {loading ? "Saving…" : "Log application"}
        </button>
        <button type="button" onClick={onDone} className="text-sm text-gray-500 hover:text-gray-700">
          Cancel
        </button>
      </div>
    </form>
  );
}

export function ApplicationsPage() {
  const [searchParams] = useSearchParams();
  const initialStatus = searchParams.get("status") ?? "All";

  const [activeTab, setActiveTab] = useState(
    STATUS_TABS.includes(initialStatus) ? initialStatus : "All"
  );
  const [showLogForm, setShowLogForm] = useState(false);

  const { data, error, loading, reload } = useApplications({
    status: activeTab === "All" ? undefined : activeTab,
    pageSize: 100,
  });
  const apps = data?.items ?? [];
  const total = data?.total ?? 0;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h2 className="flex items-center text-lg font-semibold text-gray-700">
          Applications
          <InfoTooltip text="Statuses update automatically when we detect a status-changing email, in order: Applied, Acknowledged, Screening, Interviewing, FinalRound, then Offer or Rejected. Ghosted/Withdrawn are set manually. You can always change a status yourself, out of order if needed." />
        </h2>
        <div className="flex items-center gap-3">
          <span className="text-sm text-gray-400">{total} total</span>
          {!showLogForm && (
            <button
              onClick={() => setShowLogForm(true)}
              className="rounded-lg bg-blue-600 px-3 py-1.5 text-sm font-medium text-white transition-colors hover:bg-blue-700"
            >
              Log an application
            </button>
          )}
        </div>
      </div>
      <PageTagline>Every application you've made, and where it stands.</PageTagline>

      {showLogForm && (
        <LogApplicationForm onDone={() => { setShowLogForm(false); reload(); }} />
      )}

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
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {apps.map(app => <ApplicationCard key={app.id} app={app} onStatusChanged={reload} />)}
        </div>
      )}
    </div>
  );
}
