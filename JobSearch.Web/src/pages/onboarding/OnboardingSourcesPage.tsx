import { OnboardingShell } from "../../components/OnboardingShell";
import { SourcesPage } from "../SourcesPage";

export function OnboardingSourcesPage({ tier }: { tier: string }) {
  return (
    <OnboardingShell
      step={3}
      tier={tier}
      title="Last step — where should we look?"
      blurb="Pick where postings come from and how you want your applications tracked. This runs quietly in the background from here on, so set it once and let it work for you."
    >
      <SourcesPage hideHeader />
    </OnboardingShell>
  );
}
