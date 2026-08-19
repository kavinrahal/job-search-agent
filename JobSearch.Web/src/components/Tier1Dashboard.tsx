import { Link } from "react-router-dom";
import { useMe } from "../hooks/useAuth";
import { GeneratePage } from "../pages/GeneratePage";
import { InfoTooltip } from "./InfoTooltip";

// Generate is Tier1's main feature, so it's embedded here directly (the real component,
// not a link to it) as the primary content — /generate still exists as its own route too,
// mainly for Tier2 users, for whom the dashboard is KPI-focused instead.
export function Tier1Dashboard() {
  const { data: me } = useMe();

  return (
    <div className="space-y-6">
      {me && (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-gray-200 bg-white px-5 py-3 shadow-sm">
          <p className="flex items-center text-sm text-gray-500">
            <span className="font-semibold text-gray-800">{me.creditBalance}</span>&nbsp;credits remaining
            <InfoTooltip text="Each CV, cover letter, or answer you generate or revise uses one credit." />
          </p>
          <div className="flex items-center gap-4 text-sm">
            <Link to="/profile" className="text-gray-500 hover:text-gray-700">Profile</Link>
            <Link to="/criteria" className="text-gray-500 hover:text-gray-700">Criteria</Link>
          </div>
        </div>
      )}

      <GeneratePage />

      <div className="rounded-xl border border-blue-200 bg-blue-50 p-5 shadow-sm">
        <p className="mb-1 text-sm font-medium text-blue-700">Tier 2 (Beta)</p>
        <p className="mb-3 text-sm text-gray-600">
          Unlock automatic job discovery, application tracking, and inbox alert forwarding.
          Free while the beta is running, no payment required yet.
        </p>
        <Link
          to="/settings"
          className="inline-block rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700"
        >
          Upgrade to Tier 2
        </Link>
      </div>
    </div>
  );
}
