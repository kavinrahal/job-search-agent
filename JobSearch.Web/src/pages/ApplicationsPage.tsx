import { useState, type FormEvent } from "react";
import { useSearchParams } from "react-router-dom";
import {
  useApplications,
  useApplicationEvents,
  useCreateApplication,
  useUpdateApplicationStatus,
} from "../hooks/useDashboardData";
import { APPLICATION_STATUSES, type Application, type ApplicationWithEvents } from "../types";
import {
  Surface,
  Button,
  Callout,
  EmptyState,
  Input,
  SegmentedControl,
  SkeletonList,
  Tooltip,
  ChecklistIcon,
  cx,
} from "../ui";

// Same tone mapping as Tier2Dashboard's status Badge, applied directly to the native <select>
// below rather than through Badge itself — a select needs to stay a real <select> for the
// platform picker, so it borrows the token classes instead of the component.
const STATUS_TONE: Record<string, string> = {
  Applied: "bg-shell text-muted",
  Acknowledged: "bg-shell text-muted",
  Screening: "bg-brass-wash text-brass",
  Interviewing: "bg-brass-wash text-brass",
  FinalRound: "bg-pos-wash text-pos",
  Offer: "bg-pos-wash text-pos",
  Rejected: "bg-ember-wash text-ember",
  Ghosted: "bg-sunk text-faint",
  Withdrawn: "bg-sunk text-faint",
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
      // status is a backend application-status enum value, and the ?? fallback already covers
      // anything outside the known set (same call as Tier2Dashboard/DiscoveriesPage's lookups).
      // eslint-disable-next-line security/detect-object-injection
      className={cx("rounded-pill border-0 px-2.5 py-[3px] text-caption font-[650] focus-ring", STATUS_TONE[status] ?? "bg-shell text-muted")}
    >
      {APPLICATION_STATUSES.map(s => <option key={s} value={s}>{s}</option>)}
    </select>
  );
}

function EventTimeline({ data }: { data: ApplicationWithEvents }) {
  return (
    <div className="hairline-t mt-3 pt-3">
      <p className="mb-2 text-eyebrow font-bold tracking-[.06em] text-faint uppercase">Timeline</p>
      <ol className="space-y-2">
        {data.events.map(ev => (
          <li key={ev.id} className="flex gap-3 text-body">
            <span className="mt-0.5 text-faint">•</span>
            <div>
              <span className="font-[650] text-ink-2">{ev.summary}</span>
              {ev.fromStatus && ev.toStatus && (
                <span className="ml-2 text-caption text-faint">
                  {ev.fromStatus} → {ev.toStatus}
                </span>
              )}
              <p className="text-caption text-faint">
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
    <Surface elevation="raised">
      <button
        onClick={toggle}
        className="flex w-full items-start justify-between gap-4 text-left"
      >
        <div className="min-w-0">
          <p className="m-0 font-[650] text-ink">{app.company}</p>
          <p className="m-0 mt-0.5 truncate text-body text-muted">{app.roleTitle || "-"}</p>
        </div>
        <div className="flex shrink-0 flex-col items-end gap-1">
          <StatusSelect status={app.status} onChange={handleStatusChange} />
          <span className="text-caption text-faint">
            Updated {new Date(app.updatedAt).toLocaleDateString("en-AU", {
              day: "2-digit", month: "short",
            })}
          </span>
        </div>
      </button>

      {expanded && (
        loading
          ? <p className="mt-3 text-body text-faint">Loading…</p>
          : detail && <EventTimeline data={detail} />
      )}
    </Surface>
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
    <form onSubmit={handleSubmit} className="surface-shell-e1 animate-fade-in-up">
      <div className="surface-core grid grid-cols-1 gap-3 p-4 sm:grid-cols-2">
        <Input
          label="Company"
          value={company}
          onChange={e => {
            setCompany(e.target.value);
            if (!companyDomain) setCompanyDomain(guessDomain(e.target.value));
          }}
          required
        />
        <Input
          label="Role title"
          value={roleTitle}
          onChange={e => setRoleTitle(e.target.value)}
          required
        />
        <Input
          label="Job URL (optional)"
          value={jobUrl}
          onChange={e => setJobUrl(e.target.value)}
        />
        <Input
          label="Company email domain (optional)"
          value={companyDomain}
          onChange={e => setCompanyDomain(e.target.value)}
          placeholder="acmecorp.com"
          hint="Only used if you're on filter-only tracking. Installs a Gmail filter forwarding mail from this domain. A rough guess, not verified, check it's right."
        />
      </div>
      {error && <div className="px-4 pb-2"><Callout variant="danger" title={error} /></div>}
      <div className="flex items-center gap-3 px-4 pb-4">
        <Button type="submit" disabled={loading} loading={loading}>
          {loading ? "Saving…" : "Log application"}
        </Button>
        <button type="button" onClick={onDone} className="text-body text-muted transition-colors hover:text-ink">
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
      <div className="mb-3.5 flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h1 className="m-0 flex items-center text-display font-bold text-ink">
            Applications
            <Tooltip text="Statuses update automatically when we detect a status-changing email, in order: Applied, Acknowledged, Screening, Interviewing, FinalRound, then Offer or Rejected. Ghosted/Withdrawn are set manually. You can always change a status yourself, out of order if needed." />
          </h1>
          <p className="m-0 text-caption text-faint">Every application you've made, and where it stands.</p>
        </div>
        <div className="flex flex-none items-center gap-3">
          <span className="text-caption text-faint">{total} total</span>
          {!showLogForm && (
            <Button size="sm" onClick={() => setShowLogForm(true)}>Log an application</Button>
          )}
        </div>
      </div>

      {showLogForm && (
        <LogApplicationForm onDone={() => { setShowLogForm(false); reload(); }} />
      )}

      <SegmentedControl
        label="Filter by status"
        segments={STATUS_TABS.map(tab => ({ value: tab, label: tab }))}
        value={activeTab}
        onChange={setActiveTab}
      />

      {error && <Callout variant="danger" title={error} />}

      {loading ? (
        <Surface elevation="raised">
          <SkeletonList rows={4} label="Loading applications" />
        </Surface>
      ) : apps.length === 0 ? (
        <Surface elevation="raised">
          <EmptyState
            icon={<ChecklistIcon />}
            title="Nothing tracked yet"
            body={
              activeTab === "All"
                ? "Log your first application to start tracking it here."
                : `Nothing with status "${activeTab}" yet — it'll show up here once an application reaches that stage.`
            }
          />
        </Surface>
      ) : (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {apps.map(app => <ApplicationCard key={app.id} app={app} onStatusChanged={reload} />)}
        </div>
      )}
    </div>
  );
}
