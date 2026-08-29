import { useState, type FormEvent, type ReactNode } from "react";
import { useApplications, useCreateApplication, useUpdateApplicationStatus } from "../hooks/useDashboardData";
import { APPLICATION_STATUSES, type Application } from "../types";
import {
  Button,
  Callout,
  cx,
  EmptyState,
  Input,
  Ledger,
  LedgerGroup,
  LedgerRow,
  SegmentedControl,
  Select,
  SkeletonList,
  Surface,
  ChecklistIcon,
  PlusIcon,
  LiveStatusIcon,
  InterviewingStatusIcon,
  ClosedStatusIcon,
  SuccessfulStatusIcon,
  type BadgeVariant,
} from "../ui";

// ---------------------------------------------------------------------------
// Status grouping — the four semantic buckets the filter (and the prototype's
// "All 12 / Live 3 / Interviewing 2 / Closed 7") key off. Every APPLICATION_STATUSES
// value lands in exactly one.
// ---------------------------------------------------------------------------
type Tab = "all" | "live" | "interviewing" | "closed" | "successful";

const TAB_LABEL: Record<Tab, string> = {
  all: "All",
  live: "Live",
  interviewing: "Interviewing",
  closed: "Closed",
  successful: "Successful",
};

const INTERVIEWING_STATUSES = new Set(["Screening", "Interviewing", "FinalRound"]);
const CLOSED_STATUSES = new Set(["Rejected", "Ghosted", "Withdrawn"]);
const SUCCESSFUL_STATUSES = new Set(["Offer"]);

function tabFor(status: string): Exclude<Tab, "all"> {
  if (INTERVIEWING_STATUSES.has(status)) return "interviewing";
  if (SUCCESSFUL_STATUSES.has(status)) return "successful";
  if (CLOSED_STATUSES.has(status)) return "closed";
  return "live"; // Applied, Acknowledged, and anything unrecognized
}

// Offer is the positive/successful outcome, so it gets the "strong" (pos/green) treatment;
// every other closed status is the neutral grey "weak" variant, and the interview funnel is
// the ember "live" one.
const STATUS_BADGE: Record<string, BadgeVariant> = {
  Offer: "strong",
  Screening: "live",
  Interviewing: "live",
  FinalRound: "live",
};
function statusBadge(status: string): BadgeVariant {
  // eslint-disable-next-line security/detect-object-injection
  return STATUS_BADGE[status] ?? "weak";
}

// The inline status Select's text color, echoing the same semantics the old read-only Badge
// carried: Offer green, the interview funnel ember, everything else the quiet neutral tone.
function statusSelectTextClass(status: string): string {
  switch (statusBadge(status)) {
    case "strong": return "text-pos!";
    case "live": return "text-ember!";
    default: return "text-faint!";
  }
}

// The Kit A status glyph for each semantic bucket, keyed off the same tabFor() bucketing the
// filter uses. Rendered as a leading glyph on the filter tabs and inline status Select.
const BUCKET_ICON: Record<Exclude<Tab, "all">, (p: { className?: string }) => ReactNode> = {
  live: LiveStatusIcon,
  interviewing: InterviewingStatusIcon,
  closed: ClosedStatusIcon,
  successful: SuccessfulStatusIcon,
};
function statusIcon(status: string, className?: string): ReactNode {
  const Glyph = BUCKET_ICON[tabFor(status)];
  return <Glyph className={className} />;
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
  const [status, setStatus] = useState<string>("Applied");

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
      status,
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
        <Select label="Status" value={status} onChange={e => setStatus(e.target.value)}>
          {APPLICATION_STATUSES.map(s => <option key={s} value={s}>{s}</option>)}
        </Select>
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

// Status is editable inline via a compact native select — there's no per-application detail
// view to push editing into, so this is where it lives. Selecting a new value PATCHes it
// straight away and reloads the list so the row's tab/tick catch up with the new status.
function ApplicationRow({ app, reload }: { app: Application; reload: () => void }) {
  const { execute } = useUpdateApplicationStatus();

  async function handleChange(newStatus: string) {
    if (newStatus === app.status) return;
    await execute(app.id, newStatus);
    reload();
  }

  return (
    <LedgerRow
      tickIcon={statusIcon(app.status, cx("h-4 w-4", statusSelectTextClass(app.status)))}
      title={app.company}
      subtitle={app.roleTitle || undefined}
      meta={
        <>
          <Select
            label={`Status for ${app.company}`}
            hideLabel
            value={app.status}
            onChange={e => handleChange(e.target.value)}
            className={cx("w-auto! py-[3px]! pr-7! text-caption! font-[650]", statusSelectTextClass(app.status))}
          >
            {APPLICATION_STATUSES.map(s => <option key={s} value={s}>{s}</option>)}
          </Select>
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
    successful: all.filter(a => tabFor(a.status) === "successful").length,
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
            { value: "live", label: "Live", count: counts.live, icon: <LiveStatusIcon /> },
            { value: "interviewing", label: "Interviewing", count: counts.interviewing, icon: <InterviewingStatusIcon /> },
            { value: "closed", label: "Closed", count: counts.closed, icon: <ClosedStatusIcon /> },
            { value: "successful", label: "Successful", count: counts.successful, icon: <SuccessfulStatusIcon /> },
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
                {thisWeek.map(app => <ApplicationRow key={app.id} app={app} reload={reload} />)}
              </>
            )}
            {earlier.length > 0 && (
              <>
                <LedgerGroup>Earlier</LedgerGroup>
                {earlier.map(app => <ApplicationRow key={app.id} app={app} reload={reload} />)}
              </>
            )}
          </Ledger>
        </Surface>
      )}
    </div>
  );
}
