import type { Summary } from "../types";

const CATEGORY_LABELS: Record<string, string> = {
  application_confirmation: "Application confirmed",
  rejection: "Rejection",
  interview_invitation: "Interview invite",
  recruiter_outreach: "Recruiter outreach",
  scheduling_request: "Scheduling request",
  offer: "Offer",
  follow_up_needed: "Action needed",
  not_relevant: "Not relevant",
};

const STATUS_COLORS: Record<string, string> = {
  Applied:      "bg-blue-50 text-blue-700 hover:bg-blue-100",
  Acknowledged: "bg-indigo-50 text-indigo-700 hover:bg-indigo-100",
  Screening:    "bg-purple-50 text-purple-700 hover:bg-purple-100",
  Interviewing: "bg-amber-50 text-amber-700 hover:bg-amber-100",
  FinalRound:   "bg-orange-50 text-orange-700 hover:bg-orange-100",
  Offer:        "bg-emerald-50 text-emerald-700 hover:bg-emerald-100",
  Rejected:     "bg-red-50 text-red-600 hover:bg-red-100",
  Ghosted:      "bg-gray-100 text-gray-500 hover:bg-gray-200",
  Withdrawn:    "bg-gray-100 text-gray-500 hover:bg-gray-200",
};

interface Props {
  summary: Summary;
  onTotalClick?: () => void;
  onJobRelatedClick?: () => void;
  onApplicationsClick?: () => void;
  onCategoryClick?: (cat: string) => void;
  onStatusClick?: (status: string) => void;
}

export function SummaryCards({
  summary,
  onTotalClick,
  onJobRelatedClick,
  onApplicationsClick,
  onCategoryClick,
  onStatusClick,
}: Props) {
  const classifyRate =
    summary.total > 0 ? Math.round((summary.classified / summary.total) * 100) : 0;

  return (
    <div className="space-y-6">
      {/* Email stats */}
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        <StatCard
          label="Total emails"
          value={summary.total}
          onClick={onTotalClick}
          title="Show all emails"
        />
        <StatCard
          label="Classified"
          value={`${summary.classified} (${classifyRate}%)`}
        />
        <StatCard
          label="Job-related"
          value={summary.jobRelated}
          highlight
          onClick={onJobRelatedClick}
          title="Filter to job-related emails"
        />
        <StatCard
          label="Applications"
          value={summary.applications.total}
          highlight
          onClick={onApplicationsClick}
          title="Go to Applications"
        />
      </div>

      {/* Category breakdown */}
      {Object.keys(summary.byCategory).length > 0 && (
        <div>
          <p className="mb-3 text-xs font-semibold uppercase tracking-wide text-gray-400">
            By category
          </p>
          <div className="flex flex-wrap gap-2">
            {Object.entries(summary.byCategory)
              .sort((a, b) => b[1] - a[1])
              .map(([cat, count]) => (
                <button
                  key={cat}
                  onClick={() => onCategoryClick?.(cat)}
                  className="rounded-full bg-blue-50 px-3 py-1 text-sm font-medium text-blue-700 transition-colors hover:bg-blue-100"
                  title={`Filter emails to "${CATEGORY_LABELS[cat] ?? cat}"`}
                >
                  {CATEGORY_LABELS[cat] ?? cat} — {count}
                </button>
              ))}
          </div>
        </div>
      )}

      {/* Application status breakdown */}
      {summary.applications.total > 0 && (
        <div>
          <p className="mb-3 text-xs font-semibold uppercase tracking-wide text-gray-400">
            Applications by status
          </p>
          <div className="flex flex-wrap gap-2">
            {Object.entries(summary.applications.byStatus)
              .sort((a, b) => b[1] - a[1])
              .map(([status, count]) => (
                <button
                  key={status}
                  onClick={() => onStatusClick?.(status)}
                  className={`rounded-full px-3 py-1 text-sm font-medium transition-colors ${STATUS_COLORS[status] ?? "bg-gray-100 text-gray-600 hover:bg-gray-200"}`}
                  title={`View ${status} applications`}
                >
                  {status} — {count}
                </button>
              ))}
          </div>
        </div>
      )}
    </div>
  );
}

function StatCard({ label, value, highlight = false, onClick, title }: {
  label: string;
  value: string | number;
  highlight?: boolean;
  onClick?: () => void;
  title?: string;
}) {
  const base = "rounded-xl border border-gray-200 bg-white p-4 shadow-sm w-full text-left";
  const interactive = onClick
    ? "cursor-pointer hover:shadow-md hover:border-blue-200 transition-shadow"
    : "";

  return (
    <div
      role={onClick ? "button" : undefined}
      tabIndex={onClick ? 0 : undefined}
      title={title}
      onClick={onClick}
      onKeyDown={e => { if (onClick && (e.key === "Enter" || e.key === " ")) onClick(); }}
      className={`${base} ${interactive}`}
    >
      <p className="text-xs font-medium uppercase tracking-wide text-gray-400">{label}</p>
      <p className={`mt-1 text-2xl font-bold ${highlight ? "text-blue-600" : "text-gray-800"}`}>
        {value}
      </p>
    </div>
  );
}
