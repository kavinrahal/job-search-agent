import { useHealth } from "../hooks/useDashboardData";

function StatRow({ label, value }: { label: string; value: string | number | null }) {
  return (
    <div className="flex items-center justify-between py-2.5">
      <span className="text-sm text-gray-500">{label}</span>
      <span className="text-sm font-medium text-gray-800">{value ?? "—"}</span>
    </div>
  );
}

export function HealthPage() {
  const { data: health, error } = useHealth();

  const statusConfig = {
    ok:      { label: "Healthy",  bg: "bg-emerald-50",  border: "border-emerald-200", text: "text-emerald-700", dot: "bg-emerald-500" },
    stale:   { label: "Stale",    bg: "bg-amber-50",    border: "border-amber-200",   text: "text-amber-700",   dot: "bg-amber-500"   },
    unknown: { label: "Unknown",  bg: "bg-gray-50",     border: "border-gray-200",    text: "text-gray-600",    dot: "bg-gray-400"    },
  };

  const cfg = health ? (statusConfig[health.status] ?? statusConfig.unknown) : null;

  return (
    <div className="space-y-6">
      <h2 className="text-lg font-semibold text-gray-700">System Health</h2>

      {error && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">{error}</div>
      )}

      {!health && !error && (
        <div className="py-12 text-center text-sm text-gray-400">Loading…</div>
      )}

      {health && cfg && (
        <div className="space-y-4">
          {/* Status banner */}
          <div className={`flex items-center gap-3 rounded-xl border ${cfg.border} ${cfg.bg} px-5 py-4`}>
            <span className={`h-3 w-3 rounded-full ${cfg.dot}`} />
            <div>
              <p className={`font-semibold ${cfg.text}`}>{cfg.label}</p>
              {health.lastRunAt && (
                <p className="text-sm text-gray-500">
                  Last run {health.lastRunAgeMinutes != null
                    ? `${Math.round(health.lastRunAgeMinutes)} min ago`
                    : "unknown"}
                  {" · "}
                  {new Date(health.lastRunAt).toLocaleString("en-AU", {
                    day: "2-digit", month: "short", year: "numeric",
                    hour: "2-digit", minute: "2-digit",
                  })}
                </p>
              )}
            </div>
          </div>

          {/* Last run stats */}
          <div className="rounded-xl border border-gray-200 bg-white shadow-sm">
            <p className="border-b border-gray-100 px-5 py-3 text-xs font-semibold uppercase tracking-wide text-gray-400">
              Last run
            </p>
            <div className="divide-y divide-gray-50 px-5">
              <StatRow label="Emails fetched"   value={health.emailsFetched} />
              <StatRow label="Emails classified" value={health.emailsClassified} />
              <StatRow label="New applications"  value={health.newApplications} />
              <StatRow label="Duration"          value={health.durationMs != null ? `${(health.durationMs / 1000).toFixed(1)}s` : null} />
              {health.lastError && (
                <div className="py-2.5">
                  <p className="text-xs font-medium text-red-500">Error</p>
                  <p className="mt-0.5 text-sm text-red-700">{health.lastError}</p>
                </div>
              )}
            </div>
          </div>

          {/* Overall stats */}
          <div className="rounded-xl border border-gray-200 bg-white shadow-sm">
            <p className="border-b border-gray-100 px-5 py-3 text-xs font-semibold uppercase tracking-wide text-gray-400">
              Overall
            </p>
            <div className="divide-y divide-gray-50 px-5">
              <StatRow label="Total applications"   value={health.totalApplications} />
              <StatRow label="Pending notifications" value={health.pendingNotifications} />
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
