import { useState, type FormEvent } from "react";
import { useApplications, useCreateApplication } from "../hooks/useDashboardData";
import type { Application } from "../types";
import {
  Badge,
  Button,
  Callout,
  EmptyState,
  Input,
  Ledger,
  LedgerGroup,
  LedgerRow,
  SegmentedControl,
  SkeletonList,
  Surface,
  ChecklistIcon,
  PlusIcon,
  type BadgeVariant,
  type StatusTickState,
} from "../ui";

// ---------------------------------------------------------------------------
// Status grouping — the three semantic buckets the filter (and the prototype's
// "All 12 / Live 3 / Interviewing 2 / Closed 7") key off. Every APPLICATION_STATUSES
// value lands in exactly one.
// ---------------------------------------------------------------------------
type Tab = "all" | "live" | "interviewing" | "closed";

const TAB_LABEL: Record<Tab, string> = { all: "All", live: "Live", interviewing: "Interviewing", closed: "Closed" };

const INTERVIEWING_STATUSES = new Set(["Screening", "Interviewing", "FinalRound"]);
const CLOSED_STATUSES = new Set(["Offer", "Rejected", "Ghosted", "Withdrawn"]);

function tabFor(status: string): Exclude<Tab, "all"> {
  if (INTERVIEWING_STATUSES.has(status)) return "interviewing";
  if (CLOSED_STATUSES.has(status)) return "closed";
  return "live"; // Applied, Acknowledged, and anything unrecognized
}

// Offer is the one "closed" status that reads as good news, so it gets the brass "good"
// treatment the prototype gives it; every other status (including the rest of "closed") is the
// neutral grey "weak" variant, and the interview funnel is the ember "live" one.
const STATUS_BADGE: Record<string, BadgeVariant> = {
  Offer: "good",
  Screening: "live",
  Interviewing: "live",
  FinalRound: "live",
};
function statusBadge(status: string): BadgeVariant {
  // eslint-disable-next-line security/detect-object-injection
  return STATUS_BADGE[status] ?? "weak";
}

// The tick reads "settled" vs. "actively moving" rather than "good" vs. "bad" — only the
// interview funnel counts as live; applied-and-waiting and every closed outcome are stable.
function statusTick(status: string): StatusTickState {
  return INTERVIEWING_STATUSES.has(status) ? "live" : "done";
}

function shortDate(iso: string) {
  return new Date(iso).toLocaleDateString("en-AU", { day: "2-digit", month: "short" });
}

function isThisWeek(iso: string) {
  return Date.now() - new Date(iso).getTime() < 7 * 24 * 60 * 60 * 1000;
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
        <Input label="Role title" value={roleTitle} onChange={e => setRoleTitle(e.target.value)} required />
        <Input label="Job URL (optional)" value={jobUrl} onChange={e => setJobUrl(e.target.value)} />
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

// Read-only: status and date only. Editing a status belongs in a per-application detail view,
// which doesn't exist yet — this list only ever displays where things stand.
function ApplicationRow({ app }: { app: Application }) {
  return (
    <LedgerRow
      tick={statusTick(app.status)}
      title={app.company}
      subtitle={app.roleTitle || undefined}
      meta={
        <>
          <Badge variant={statusBadge(app.status)}>{app.status}</Badge>
          <span className="text-meta whitespace-nowrap text-faint">{shortDate(app.updatedAt)}</span>
        </>
      }
    />
  );
}

export function ApplicationsPage() {
  const [activeTab, setActiveTab] = useState<Tab>("all");
  const [showLogForm, setShowLogForm] = useState(false);

  // Fetched once, unfiltered — the semantic tabs (Live/Interviewing/Closed) each combine several
  // raw statuses, which a single server-side status filter can't express, so counting and
  // filtering both happen client-side against the one list.
  const { data, error, loading, reload } = useApplications({ pageSize: 100 });
  const all = data?.items ?? [];

  const counts = {
    all: all.length,
    live: all.filter(a => tabFor(a.status) === "live").length,
    interviewing: all.filter(a => tabFor(a.status) === "interviewing").length,
    closed: all.filter(a => tabFor(a.status) === "closed").length,
  };

  const visible = activeTab === "all" ? all : all.filter(a => tabFor(a.status) === activeTab);
  const sorted = [...visible].sort((a, b) => new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime());
  const thisWeek = sorted.filter(a => isThisWeek(a.updatedAt));
  const earlier = sorted.filter(a => !isThisWeek(a.updatedAt));

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <SegmentedControl
          label="Filter applications"
          segments={[
            { value: "all", label: "All", count: counts.all },
            { value: "live", label: "Live", count: counts.live },
            { value: "interviewing", label: "Interviewing", count: counts.interviewing },
            { value: "closed", label: "Closed", count: counts.closed },
          ]}
          value={activeTab}
          onChange={setActiveTab}
        />
        <Button size="sm" cap={<PlusIcon className="h-2.5 w-2.5" />} onClick={() => setShowLogForm(s => !s)}>
          Log application
        </Button>
      </div>

      {showLogForm && <LogApplicationForm onDone={() => { setShowLogForm(false); reload(); }} />}

      {error && <Callout variant="danger" title={error} />}

      {loading ? (
        <Surface elevation="raised">
          <SkeletonList rows={4} label="Loading applications" />
        </Surface>
      ) : all.length === 0 ? (
        <Surface elevation="raised">
          <EmptyState
            icon={<ChecklistIcon />}
            title="Nothing tracked yet"
            body="Log your first application to start tracking it here."
          />
        </Surface>
      ) : sorted.length === 0 ? (
        <Surface elevation="raised">
          <EmptyState
            icon={<ChecklistIcon />}
            title="Nothing here"
            // eslint-disable-next-line security/detect-object-injection -- activeTab is the Tab union, not arbitrary input
            body={`Nothing in "${TAB_LABEL[activeTab]}" right now — it'll show up here once an application reaches that stage.`}
          />
        </Surface>
      ) : (
        <Surface elevation="raised" padding="none" clip>
          <Ledger>
            {thisWeek.length > 0 && (
              <>
                <LedgerGroup>This week</LedgerGroup>
                {thisWeek.map(app => <ApplicationRow key={app.id} app={app} />)}
              </>
            )}
            {earlier.length > 0 && (
              <>
                <LedgerGroup>Earlier</LedgerGroup>
                {earlier.map(app => <ApplicationRow key={app.id} app={app} />)}
              </>
            )}
          </Ledger>
        </Surface>
      )}
    </div>
  );
}
