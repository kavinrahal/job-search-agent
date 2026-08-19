import { Link } from "react-router-dom";
import { useMe } from "../hooks/useAuth";
import { useProfile } from "../hooks/useProfile";
import { parseJobCriteriaYaml } from "../lib/jobCriteriaYaml";
import { Tier1Dashboard } from "../components/Tier1Dashboard";
import { Tier2Dashboard } from "../components/Tier2Dashboard";
import { DashboardGreeting } from "../components/DashboardGreeting";

// Separate from the /auth/me needsCriteria gate (App.tsx), which only checks that the user
// has visited and saved the Criteria page once — saving always writes a full YAML skeleton
// with defaults, so that flag alone can't tell whether anything meaningful was entered. This
// checks the actual parsed content instead, and keeps nudging until skill dimensions exist.
function CriteriaNudge() {
  const { data: profile } = useProfile();
  if (!profile || parseJobCriteriaYaml(profile.jobCriteria).skillDimensions.length > 0) return null;

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800 dark:border-amber-900/50 dark:bg-amber-500/10 dark:text-amber-300">
      <p>
        Your job criteria isn't set yet. It's what makes Tier 2's automatic matching accurate,
        worth filling in now even before you upgrade.
      </p>
      <Link
        to="/criteria"
        className="shrink-0 rounded-lg bg-amber-600 px-3 py-1.5 text-sm font-medium text-white transition-colors duration-150 hover:bg-amber-700 dark:bg-amber-600 dark:hover:bg-amber-500"
      >
        Set criteria
      </Link>
    </div>
  );
}

export function DashboardPage() {
  const { data: me, loading } = useMe();

  if (loading || !me) return <div className="py-12 text-center text-sm text-gray-400 dark:text-gray-500">Loading…</div>;

  return (
    <div className="space-y-6">
      <DashboardGreeting name={me.firstName} />
      <CriteriaNudge />
      {me.tier === "Tier2" ? <Tier2Dashboard /> : <Tier1Dashboard />}
    </div>
  );
}
