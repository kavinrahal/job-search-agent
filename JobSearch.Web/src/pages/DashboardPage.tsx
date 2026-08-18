import { useMe } from "../hooks/useAuth";
import { Tier1Dashboard } from "../components/Tier1Dashboard";
import { Tier2Dashboard } from "../components/Tier2Dashboard";

export function DashboardPage() {
  const { data: me, loading } = useMe();

  if (loading || !me) return <div className="py-12 text-center text-sm text-gray-400">Loading…</div>;

  return me.tier === "Tier2" ? <Tier2Dashboard /> : <Tier1Dashboard />;
}
