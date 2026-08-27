import type { ReactNode } from "react";
import { StepIndicator, PageHeader } from "../ui";

const TIER1_STEPS = ["Your background", "Job criteria"];
const TIER2_STEPS = ["Your background", "Job criteria", "Sources"];

// The shell around each onboarding step (see pages/onboarding/) — a step indicator, an
// encouraging headline, and a short "why this matters" blurb, wrapping the same form
// component used later from the persistent nav (ResumeIntakePage/JobCriteriaPage/
// SourcesPage, rendered with hideHeader so their own plainer heading doesn't double up
// with this one). First impression for a brand new user, not a permanent page.
export function OnboardingShell({ step, tier, title, blurb, children }: {
  step: number; tier: string; title: string; blurb: string; children: ReactNode;
}) {
  const steps = (tier === "Tier2" ? TIER2_STEPS : TIER1_STEPS).map(label => ({ label }));
  return (
    <div className="mx-auto max-w-2xl space-y-8">
      <StepIndicator steps={steps} current={step - 1} />
      <PageHeader title={title} tagline={blurb} />
      {children}
    </div>
  );
}
