import { useMe } from "../hooks/useAuth";
import { useProfile } from "../hooks/useProfile";
import { parseJobCriteriaYaml } from "../lib/jobCriteriaYaml";
import { getMissingCriteriaFields } from "../lib/criteriaCompleteness";
import { Tier1Dashboard } from "../components/Tier1Dashboard";
import { Tier2Dashboard } from "../components/Tier2Dashboard";
import { DashboardGreeting } from "../components/DashboardGreeting";
import { Button, Callout } from "../ui";

// Separate from the /auth/me needsCriteria gate (App.tsx), which only checks that the user
// has visited and saved the Criteria page once — saving always writes a full YAML skeleton
// with defaults, so that flag alone can't tell whether anything meaningful was entered. Now
// that the same mandatory fields block Save on the Criteria page itself, this should rarely
// fire for anyone who saves going forward — it's a safety net for accounts that had partial
// criteria saved before that enforcement existed.
function CriteriaNudge({ tier }: { tier: string }) {
  const { data: profile } = useProfile();
  if (!profile) return null;
  const missing = getMissingCriteriaFields(parseJobCriteriaYaml(profile.jobCriteria), tier);
  if (missing.length === 0) return null;

  return (
    <div className="flex flex-wrap items-center justify-between gap-3">
      <Callout
        variant="warning"
        title="Your job criteria is incomplete."
        className="flex-1"
      >
        Missing: {missing.map(m => m.label).join(", ")}. It's what makes Tier 2's automatic
        matching accurate, worth filling in now even before you upgrade.
      </Callout>
      <Button href="/criteria" size="sm" className="shrink-0">Set criteria</Button>
    </div>
  );
}

export function DashboardPage() {
  const { data: me, loading } = useMe();

  if (loading || !me) return <div className="py-12 text-center text-caption text-faint">Loading…</div>;

  return (
    <div className="space-y-6">
      <DashboardGreeting name={me.firstName} />
      <CriteriaNudge tier={me.tier} />
      {me.tier === "Tier2" ? <Tier2Dashboard /> : <Tier1Dashboard />}
    </div>
  );
}
