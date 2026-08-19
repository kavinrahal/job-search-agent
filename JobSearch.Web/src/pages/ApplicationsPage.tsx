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
import { PRIMARY_BUTTON, PRIMARY_BUTTON_SM } from "../lib/styles";

const STATUS_COLORS: Record<string, string> = {
  Applied:      "bg-blue-100 text-blue-700 dark:bg-blue-500/15 dark:text-blue-300",
  Acknowledged: "bg-indigo-100 text-indigo-700 dark:bg-indigo-500/15 dark:text-indigo-300",
  Screening:    "bg-purple-100 text-purple-700 dark:bg-purple-500/15 dark:text-purple-300",
  Interviewing: "bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300",
  FinalRound:   "bg-orange-100 text-orange-700 dark:bg-orange-500/15 dark:text-orange-300",
  Offer:        "bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300",
  Rejected:     "bg-red-100 text-red-700 dark:bg-red-500/15 dark:text-red-400",
  Ghosted:      "bg-gray-100 text-gray-500 dark:bg-gray-800 dark:text-gray-400",
  Withdrawn:    "bg-gray-100 text-gray-500 dark:bg-gray-800 dark:text-gray-400",
};

const STATUS_TABS = ["All", "Applied", "Acknowledged", "Screening", "Interviewing", "FinalRound", "Offer", "Rejected"];
// Unpadded — both usages below add their own (form padding vs. empty-state padding), unlike
// the shared CARD in lib/styles.ts which bakes in p-5 for the common case.
const CARD = "rounded-xl border border-gray-200 bg-white shadow-sm dark:border-gray-800 dark:bg-gray-900";
const FIELD_INPUT = "w-full rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-violet-400 dark:border-gray-700 dark:bg-gray-800 dark:text-gray-100 dark:focus:ring-violet-500";

// Fallback for anything automatic tracking misses — a plain <select> styled to still read as
// a colored status pill. Stops propagation so picking a status doesn't also toggle the card.
function StatusSelect({ status, onChange }: { status: string; onChange: (next: string) => void }) {
  return (
    <select
      value={status}
      onClick={e => e.stopPropagation()}
      onChange={e => onChange(e.target.value)}
      className={`rounded-full border-0 px-2 py-0.5 text-xs font-medium ${STATUS_COLORS[status] ?? "bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-300"}`}
    >
      {APPLICATION_STATUSES.map(s => <option key={s} value={s}>{s}</option>)}
    </select>
  );
}

function EventTimeline({ data }: { data: ApplicationWithEvents }) {
  return (
    <div className="mt-3 border-t border-gray-100 pt-3 dark:border-gray-800">
      <p className="mb-2 text-xs font-semibold uppercase tracking-wide text-gray-400 dark:text-gray-500">Timeline</p>
      <ol className="space-y-2">
        {data.events.map(ev => (
          <li key={ev.id} className="flex gap-3 text-sm">
            <span className="mt-0.5 text-gray-300 dark:text-gray-600">•</span>
            <div>
              <span className="font-medium text-gray-700 dark:text-gray-200">{ev.summary}</span>
              {ev.fromStatus && ev.toStatus && (
                <span className="ml-2 text-xs text-gray-400 dark:text-gray-500">
                  {ev.fromStatus} → {ev.toStatus}
                </span>
              )}
              <p className="text-xs text-gray-400 dark:text-gray-500">
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
    <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm transition-shadow duration-150 hover:shadow-md dark:border-gray-800 dark:bg-gray-900 dark:hover:border-gray-700 dark:hover:shadow-none">
      <button
        onClick={toggle}
        className="flex w-full items-start justify-between gap-4 text-left"
      >
        <div className="min-w-0">
          <p className="font-semibold text-gray-800 dark:text-gray-100">{app.company}</p>
          <p className="mt-0.5 truncate text-sm text-gray-500 dark:text-gray-400">{app.roleTitle || "-"}</p>
        </div>
        <div className="flex shrink-0 flex-col items-end gap-1">
          <StatusSelect status={app.status} onChange={handleStatusChange} />
          <span className="text-xs text-gray-400 dark:text-gray-500">
            Updated {new Date(app.updatedAt).toLocaleDateString("en-AU", {
              day: "2-digit", month: "short",
            })}
          </span>
        </div>
      </button>

      {expanded && (
        loading
          ? <p className="mt-3 text-sm text-gray-400 dark:text-gray-500">Loading…</p>
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
    <form onSubmit={handleSubmit} className={`${CARD} animate-fade-in-up p-4`}>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <div>
          <label className="mb-1 block text-xs font-medium text-gray-500 dark:text-gray-400">Company</label>
          <input
            value={company}
            onChange={e => {
              setCompany(e.target.value);
              if (!companyDomain) setCompanyDomain(guessDomain(e.target.value));
            }}
            required
            className={FIELD_INPUT}
          />
        </div>
        <div>
          <label className="mb-1 block text-xs font-medium text-gray-500 dark:text-gray-400">Role title</label>
          <input
            value={roleTitle}
            onChange={e => setRoleTitle(e.target.value)}
            required
            className={FIELD_INPUT}
          />
        </div>
        <div>
          <label className="mb-1 block text-xs font-medium text-gray-500 dark:text-gray-400">Job URL (optional)</label>
          <input
            value={jobUrl}
            onChange={e => setJobUrl(e.target.value)}
            className={FIELD_INPUT}
          />
        </div>
        <div>
          <label className="mb-1 block text-xs font-medium text-gray-500 dark:text-gray-400">
            Company email domain (optional)
          </label>
          <input
            value={companyDomain}
            onChange={e => setCompanyDomain(e.target.value)}
            placeholder="acmecorp.com"
            className={FIELD_INPUT}
          />
          <p className="mt-1 text-xs text-gray-400 dark:text-gray-500">
            Only used if you're on filter-only tracking. Installs a Gmail filter forwarding
            mail from this domain. A rough guess, not verified, check it's right.
          </p>
        </div>
      </div>
      {error && <p className="mt-2 text-sm text-red-700 dark:text-red-400">{error}</p>}
      <div className="mt-3 flex items-center gap-3">
        <button type="submit" disabled={loading} className={PRIMARY_BUTTON}>
          {loading ? "Saving…" : "Log application"}
        </button>
        <button type="button" onClick={onDone} className="text-sm text-gray-500 transition-colors hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200">
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
        <h2 className="flex items-center text-lg font-semibold text-gray-700 dark:text-gray-200">
          Applications
          <InfoTooltip text="Statuses update automatically when we detect a status-changing email, in order: Applied, Acknowledged, Screening, Interviewing, FinalRound, then Offer or Rejected. Ghosted/Withdrawn are set manually. You can always change a status yourself, out of order if needed." />
        </h2>
        <div className="flex items-center gap-3">
          <span className="text-sm text-gray-400 dark:text-gray-500">{total} total</span>
          {!showLogForm && (
            <button onClick={() => setShowLogForm(true)} className={PRIMARY_BUTTON_SM}>
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
            className={`rounded-full px-3 py-1 text-sm font-medium transition-colors duration-150 ${
              activeTab === tab
                ? "bg-gradient-to-r from-violet-600 to-fuchsia-500 text-white shadow-sm shadow-violet-600/20"
                : "border border-gray-200 bg-white text-gray-600 hover:bg-gray-50 dark:border-gray-700 dark:bg-gray-900 dark:text-gray-300 dark:hover:bg-gray-800"
            }`}
          >
            {tab}
          </button>
        ))}
      </div>

      {error && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-950/30 dark:text-red-400">{error}</div>
      )}

      {loading ? (
        <div className="py-12 text-center text-sm text-gray-400 dark:text-gray-500">Loading…</div>
      ) : apps.length === 0 ? (
        <div className={`${CARD} py-12 text-center text-sm text-gray-400 dark:text-gray-500`}>
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
