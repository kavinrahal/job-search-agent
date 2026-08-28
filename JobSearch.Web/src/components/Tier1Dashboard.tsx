import { Link } from "react-router-dom";
import { useMe } from "../hooks/useAuth";
import { GeneratePage } from "../pages/GeneratePage";
import { Surface, Button, Callout, Tooltip } from "../ui";

// Generate is Tier1's main feature, so it's embedded here directly (the real component,
// not a link to it) as the primary content — /generate still exists as its own route too,
// mainly for Tier2 users, for whom the dashboard is KPI-focused instead.
export function Tier1Dashboard() {
  const { data: me } = useMe();

  return (
    <div className="space-y-6">
      {me && (
        <Surface elevation="raised" padding="sm">
          {/* Surface's `className` lands on its outer shell div (one child: the padded core), so
              the row layout has to live on a wrapper inside the actual children instead. */}
          <div className="flex flex-wrap items-center justify-between gap-3">
            <p className="flex items-center text-body text-muted">
              <span className="font-bold text-ink">{me.creditBalance}</span>&nbsp;credits remaining
              <Tooltip text="Each CV, cover letter, or answer you generate or revise uses one credit." />
            </p>
            <div className="flex items-center gap-4 text-body">
              <Link to="/profile" className="text-muted transition-colors hover:text-ink">Profile</Link>
              <Link to="/criteria" className="text-muted transition-colors hover:text-ink">Criteria</Link>
            </div>
          </div>
        </Surface>
      )}

      <GeneratePage />

      <Callout variant="warning" title="Tier 2 (Beta)">
        Unlock automatic job discovery, application tracking, and inbox alert forwarding. Free
        while the beta is running, no payment required yet.
      </Callout>
      <Button href="/settings">Upgrade to Tier 2</Button>
    </div>
  );
}
