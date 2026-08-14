import { useState } from "react";
import { useActivity } from "../hooks/useDashboardData";

const EVENT_COLORS: Record<string, string> = {
  StatusChanged: "bg-blue-100 text-blue-700",
  EmailReceived: "bg-gray-100 text-gray-600",
  ManualUpdate:  "bg-purple-100 text-purple-700",
};

const STATUS_COLORS: Record<string, string> = {
  Applied:      "text-blue-600",
  Acknowledged: "text-indigo-600",
  Screening:    "text-purple-600",
  Interviewing: "text-amber-600",
  FinalRound:   "text-orange-600",
  Offer:        "text-emerald-600",
  Rejected:     "text-red-500",
  Ghosted:      "text-gray-400",
  Withdrawn:    "text-gray-400",
};

export function ActivityPage() {
  const [limit, setLimit] = useState(30);
  const { data, error, loading } = useActivity(limit);
  const items = data ?? [];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold text-gray-700">Activity</h2>
        <select
          value={limit}
          onChange={e => setLimit(Number(e.target.value))}
          className="rounded-lg border border-gray-200 px-2 py-1.5 text-sm text-gray-600 focus:outline-none focus:ring-2 focus:ring-blue-300"
        >
          <option value={20}>Last 20</option>
          <option value={50}>Last 50</option>
          <option value={100}>Last 100</option>
        </select>
      </div>

      {error && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">{error}</div>
      )}

      {loading ? (
        <div className="py-12 text-center text-sm text-gray-400">Loading…</div>
      ) : items.length === 0 ? (
        <div className="rounded-xl border border-gray-200 bg-white py-12 text-center text-sm text-gray-400 shadow-sm">
          No activity yet.
        </div>
      ) : (
        <div className="rounded-xl border border-gray-200 bg-white shadow-sm">
          <ol className="divide-y divide-gray-50">
            {items.map((item, i) => (
              <li key={i} className="flex gap-4 px-5 py-4">
                <div className="mt-0.5 flex shrink-0 flex-col items-center">
                  <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${EVENT_COLORS[item.eventType] ?? "bg-gray-100 text-gray-600"}`}>
                    {item.eventType === "StatusChanged" ? "Status" : item.eventType === "EmailReceived" ? "Email" : "Update"}
                  </span>
                </div>

                <div className="min-w-0 flex-1">
                  <div className="flex items-baseline justify-between gap-2">
                    <p className="font-medium text-gray-800">
                      {item.company}
                      {item.roleTitle && (
                        <span className="ml-1 font-normal text-gray-400">— {item.roleTitle}</span>
                      )}
                    </p>
                    <span className="shrink-0 text-xs text-gray-400">
                      {new Date(item.occurredAt).toLocaleDateString("en-AU", {
                        day: "2-digit", month: "short", year: "numeric",
                      })}
                    </span>
                  </div>
                  <p className="mt-0.5 text-sm text-gray-600">{item.summary}</p>
                  {item.fromStatus && item.toStatus && (
                    <p className="mt-0.5 text-xs">
                      <span className={STATUS_COLORS[item.fromStatus] ?? "text-gray-400"}>{item.fromStatus}</span>
                      <span className="mx-1 text-gray-300">→</span>
                      <span className={`font-medium ${STATUS_COLORS[item.toStatus] ?? "text-gray-600"}`}>{item.toStatus}</span>
                    </p>
                  )}
                </div>
              </li>
            ))}
          </ol>
        </div>
      )}
    </div>
  );
}
