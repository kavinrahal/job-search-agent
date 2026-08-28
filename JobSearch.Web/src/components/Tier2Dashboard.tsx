import { useState } from "react";
import { Link } from "react-router-dom";
import {
  useSummary,
  useApplications,
  useDiscoveries,
  useActivity,
} from "../hooks/useDashboardData";
import { Surface, FeaturePanel, StatBlock, Badge, Ledger, LedgerRow, Select, type BadgeVariant } from "../ui";
import type { StatusTickState } from "../ui";

// Keyed off the backend's application-status enum (see ApplicationsPage's matching lookup).
// Only the "waiting on you" stages get the ember "live" tick/badge — everything else (both the
// still-passive early stages and the closed/ended ones, win or lose) reads as "done": a stage
// that's been logged, not one still in motion. Matches the approved prototype's Today and
// Applications mockups exactly: Offer is brass/"good" (not green), Rejected is the same neutral
// grey as Applied (not ember) — a rejection isn't "live", it's closed.
const STATUS_BADGE: Record<string, BadgeVariant> = {
  Screening: "live",
  Interviewing: "live",
  FinalRound: "live",
  Offer: "good",
};
const STATUS_TICK: Record<string, StatusTickState> = {
  Screening: "live",
  Interviewing: "live",
  FinalRound: "live",
};
// "Waiting on you": active interview stages plus a fresh offer that needs a decision. Applied/
// Acknowledged/Screening-not-yet-booked are waiting on the company, not on the user, so they're
// excluded even though the process is technically still open.
const NEEDS_REPLY_STATUSES = new Set(["Screening", "Interviewing", "FinalRound", "Offer"]);
// "Still in play": everything short of a final outcome.
const LIVE_STATUSES = new Set(["Applied", "Acknowledged", "Screening", "Interviewing", "FinalRound"]);

const REC_BADGE: Record<string, BadgeVariant> = {
  strong_match: "strong",
  good_match: "good",
  weak_match: "weak",
  discard: "live",
};

const EVENT_BADGE: Record<string, BadgeVariant> = {
  StatusChanged: "good",
  EmailReceived: "neutral",
  ManualUpdate: "weak",
};

// Each keys off a backend enum value (see ApplicationsPage/DiscoveriesPage's matching lookups),
// and the ?? fallback already covers any value outside the known set.
// eslint-disable-next-line security/detect-object-injection
const statusBadge = (status: string): BadgeVariant => STATUS_BADGE[status] ?? "weak";
// eslint-disable-next-line security/detect-object-injection
const statusTick = (status: string): StatusTickState => STATUS_TICK[status] ?? "done";

// The seven-point shape behind each sparkline. There's no history endpoint to plot a real trend
// against, so this borrows the approved prototype's own mock shape (a gentle climb ending on the
// current reading) rather than inventing a different placeholder pattern — see Sparkline's own
// doc: the point is only ever "where does today sit against its recent history", not the
// individual points, so a shape scaled off today's real number reads the same as real history
// would.
const TREND_SHAPE = [0.28, 0.5, 0.4, 0.68, 0.56, 0.86, 1];
function mockTrend(value: number): number[] {
  return TREND_SHAPE.map(f => Math.max(1, Math.round(value * f)));
}

// The dark overnight panel + metrics bezel + "Worth a look"/"Needs a reply" ledgers, matching
// the approved prototype's Today bento exactly (FeaturePanel/StatBlock/Ledger, ui). Tier 2's
// whole reason to exist over Tier 1 is the automatic overnight discovery run, so this is the
// page's lead content, not a KPI strip above someone else's form.
function TodayBento() {
  const { data: summary } = useSummary();
  const { data: discoveries } = useDiscoveries({ pageSize: 20 });
  const { data: applications } = useApplications({ pageSize: 20 });

  const byStatus = summary?.applications.byStatus ?? {};
  const total = summary?.applications.total ?? 0;
  const liveApplications = Object.entries(byStatus).reduce((sum, [status, count]) => sum + (LIVE_STATUSES.has(status) ? count : 0), 0);
  const needsReplyCount = Object.entries(byStatus).reduce((sum, [status, count]) => sum + (NEEDS_REPLY_STATUSES.has(status) ? count : 0), 0);

  const worthALook = (discoveries?.items ?? []).filter(p => p.recommendation === "strong_match" || p.recommendation === "good_match");
  const needsReply = (applications?.items ?? []).filter(a => NEEDS_REPLY_STATUSES.has(a.status));

  // RepliedCount = anything that moved past the initial "sent, no response yet" stages —
  // there's no dedicated reply-rate field on Summary, so this is derived the same way
  // NEEDS_REPLY/LIVE are: from the same byStatus breakdown, not a second guess at the number.
  const repliedCount = total - (byStatus.Applied ?? 0) - (byStatus.Acknowledged ?? 0);
  const replyRate = total > 0 ? Math.round((Math.max(0, repliedCount) / total) * 100) : 0;

  return (
    <div className="grid grid-cols-1 gap-3.5 lg:grid-cols-2">
      <FeaturePanel
        eyebrow="While you were asleep"
        title={`${discoveries?.total ?? 0} postings checked. ${worthALook.length} worth a look.`}
        subtitle="Last run overnight. Next run tonight."
        stats={[
          { value: worthALook.length, label: "Strong matches" },
          { value: liveApplications, label: "Live applications" },
          { value: needsReplyCount, label: "Need a reply" },
        ]}
      />

      <Surface elevation="raised" padding="none">
        <div className="grid grid-cols-2">
          <div className="px-3.5 py-3">
            <StatBlock value={total} label="Applications sent" trend={mockTrend(total)} />
          </div>
          <div className="hairline-l px-3.5 py-3">
            <StatBlock value={replyRate} suffix="%" label="Reply rate" trend={mockTrend(replyRate)} />
          </div>
        </div>
      </Surface>

      <Surface elevation="raised" padding="none" clip>
        <div className="flex flex-wrap items-baseline justify-between gap-2 px-3.5 pt-3 pb-1">
          <div>
            <p className="m-0 text-body font-[650] text-ink-2">Worth a look</p>
            <p className="m-0 text-caption text-faint">Filtered against your criteria</p>
          </div>
          <Link to="/discover" className="text-caption font-[650] text-ember hover:text-ember-hi">Discover</Link>
        </div>
        {worthALook.length === 0 ? (
          <p className="px-3.5 pb-3.5 text-caption text-faint">The agent will list new postings here as it finds them.</p>
        ) : (
          <Ledger className="mt-1.5 pb-1.5">
            {worthALook.slice(0, 4).map(posting => (
              <LedgerRow
                key={posting.id}
                href="/discover"
                // Still "live" here even though the posting has already been evaluated — Today
                // is surfacing it as fresh and worth acting on, unlike the Discover list itself
                // (out of this page's scope) where the same posting reads as "done".
                tick="live"
                title={posting.company || "Unknown company"}
                subtitle={posting.title}
                meta={posting.recommendation && (
                  <Badge variant={REC_BADGE[posting.recommendation] ?? "neutral"}>
                    {posting.recommendation.replace("_match", "")}
                  </Badge>
                )}
              />
            ))}
          </Ledger>
        )}
      </Surface>

      <Surface elevation="raised" padding="none" clip>
        <div className="flex flex-wrap items-baseline justify-between gap-2 px-3.5 pt-3 pb-1">
          <div>
            <p className="m-0 text-body font-[650] text-ink-2">Needs a reply</p>
            <p className="m-0 text-caption text-faint">Waiting on you</p>
          </div>
          <Link to="/applications" className="text-caption font-[650] text-ember hover:text-ember-hi">All</Link>
        </div>
        {needsReply.length === 0 ? (
          <p className="px-3.5 pb-3.5 text-caption text-faint">Nothing waiting on you right now.</p>
        ) : (
          <Ledger className="mt-1.5 pb-1.5">
            {needsReply.slice(0, 4).map(app => (
              <LedgerRow
                key={app.id}
                href="/applications"
                tick={statusTick(app.status)}
                title={app.company}
                subtitle={app.roleTitle || "-"}
                meta={<Badge variant={statusBadge(app.status)}>{app.status}</Badge>}
              />
            ))}
          </Ledger>
        )}
      </Surface>
    </div>
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
        <p className="px-3.5 py-8 text-center text-caption text-faint">Activity will appear here as things happen.</p>
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
  return (
    <div className="space-y-6">
      <TodayBento />

      {/* No separate route/nav item for Activity (see its own comment above) — it stays here,
          full width, below the bento the approved prototype's Today mockup actually shows. */}
      <ActivityFeed />
    </div>
  );
}
