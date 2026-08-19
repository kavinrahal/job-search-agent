import { Link } from "react-router-dom";
import { useMe } from "../hooks/useAuth";
import { GeneratePage } from "../pages/GeneratePage";
import { InfoTooltip } from "./InfoTooltip";
import { PRIMARY_BUTTON } from "../lib/styles";

// Generate is Tier1's main feature, so it's embedded here directly (the real component,
// not a link to it) as the primary content — /generate still exists as its own route too,
// mainly for Tier2 users, for whom the dashboard is KPI-focused instead.
export function Tier1Dashboard() {
  const { data: me } = useMe();

  return (
    <div className="space-y-6">
      {me && (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-gray-200 bg-white px-5 py-3 shadow-sm dark:border-gray-800 dark:bg-gray-900">
          <p className="flex items-center text-sm text-gray-500 dark:text-gray-400">
            <span className="font-semibold text-gray-800 dark:text-gray-100">{me.creditBalance}</span>&nbsp;credits remaining
            <InfoTooltip text="Each CV, cover letter, or answer you generate or revise uses one credit." />
          </p>
          <div className="flex items-center gap-4 text-sm">
            <Link to="/profile" className="text-gray-500 transition-colors hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200">Profile</Link>
            <Link to="/criteria" className="text-gray-500 transition-colors hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200">Criteria</Link>
          </div>
        </div>
      )}

      <GeneratePage />

      <div className="rounded-xl border border-violet-200 bg-gradient-to-br from-violet-50 to-fuchsia-50 p-5 shadow-sm dark:border-violet-900/50 dark:from-violet-950/40 dark:to-fuchsia-950/40">
        <p className="mb-1 text-sm font-medium text-violet-700 dark:text-violet-300">Tier 2 (Beta)</p>
        <p className="mb-3 text-sm text-gray-600 dark:text-gray-300">
          Unlock automatic job discovery, application tracking, and inbox alert forwarding.
          Free while the beta is running, no payment required yet.
        </p>
        <Link
          to="/settings"
          className={`inline-block ${PRIMARY_BUTTON}`}
        >
          Upgrade to Tier 2
        </Link>
      </div>
    </div>
  );
}
