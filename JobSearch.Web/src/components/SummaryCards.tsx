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
  Applied:      "bg-blue-50 text-blue-700",
  Acknowledged: "bg-indigo-50 text-indigo-700",
  Screening:    "bg-purple-50 text-purple-700",
  Interviewing: "bg-amber-50 text-amber-700",
  FinalRound:   "bg-orange-50 text-orange-700",
  Offer:        "bg-emerald-50 text-emerald-700",
  Rejected:     "bg-red-50 text-red-600",
  Ghosted:      "bg-gray-100 text-gray-500",
  Withdrawn:    "bg-gray-100 text-gray-500",
};

interface Props {
  summary: Summary;
}

export function SummaryCards({ summary }: Props) {
  const classifyRate =
    summary.total > 0 ? Math.round((summary.classified / summary.total) * 100) : 0;

  return (
    <div className="space-y-6">
      {/* Email stats */}
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        <StatCard label="Total emails"  value={summary.total} />
        <StatCard label="Classified"    value={`${summary.classified} (${classifyRate}%)`} />
        <StatCard label="Job-related"   value={summary.jobRelated} highlight />
        <StatCard label="Applications"  value={summary.applications.total} highlight />
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
                <span
                  key={cat}
                  className="rounded-full bg-blue-50 px-3 py-1 text-sm font-medium text-blue-700"
                >
                  {CATEGORY_LABELS[cat] ?? cat} — {count}
                </span>
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
                <span
                  key={status}
                  className={`rounded-full px-3 py-1 text-sm font-medium ${STATUS_COLORS[status] ?? "bg-gray-100 text-gray-600"}`}
                >
                  {status} — {count}
                </span>
              ))}
          </div>
        </div>
      )}
    </div>
  );
}

function StatCard({ label, value, highlight = false }: {
  label: string;
  value: string | number;
  highlight?: boolean;
}) {
  return (
    <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
      <p className="text-xs font-medium uppercase tracking-wide text-gray-400">{label}</p>
      <p className={`mt-1 text-2xl font-bold ${highlight ? "text-blue-600" : "text-gray-800"}`}>
        {value}
      </p>
    </div>
  );
}
