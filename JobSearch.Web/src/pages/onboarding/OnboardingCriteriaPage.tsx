import { OnboardingShell } from "../../components/OnboardingShell";
import { JobCriteriaPage } from "../JobCriteriaPage";

export function OnboardingCriteriaPage({ tier }: { tier: string }) {
  return (
    <OnboardingShell
      step={2}
      tier={tier}
      title="Now, what are you actually looking for?"
      blurb="This is what every posting gets measured against: salary, location, must-haves, dealbreakers. The more specific you are, the sharper the evaluations, and the fewer irrelevant matches you'll have to wade through."
    >
      {/* Hard-navigates rather than reloading in place — this route only exists during
          onboarding, so the natural next stop is "/", which re-evaluates the next required
          step (Sources for Tier 2, or straight to the dashboard for Tier 1). */}
      <JobCriteriaPage hideHeader onSaved={() => { window.location.href = "/"; }} />
    </OnboardingShell>
  );
}
