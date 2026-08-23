import { OnboardingShell } from "../../components/OnboardingShell";
import { CriteriaWizard } from "../../components/CriteriaWizard";

export function OnboardingCriteriaPage({ tier }: { tier: string }) {
  return (
    <OnboardingShell
      step={2}
      tier={tier}
      title="Now, what are you actually looking for?"
      blurb="A few quick questions, mostly buttons, no essay required. You can always add more detail later from Settings."
    >
      {/* Hard-navigates rather than reloading in place — this route only exists during
          onboarding, so the natural next stop is "/", which re-evaluates the next required
          step (Sources for Tier 2, or straight to the dashboard for Tier 1). */}
      <CriteriaWizard tier={tier} onSaved={() => { window.location.href = "/"; }} />
    </OnboardingShell>
  );
}
