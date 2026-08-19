import type { ReactNode } from "react";

const TIER1_STEPS = ["Your background", "Job criteria"];
const TIER2_STEPS = ["Your background", "Job criteria", "Sources"];

function StepDots({ step, tier }: { step: number; tier: string }) {
  const steps = tier === "Tier2" ? TIER2_STEPS : TIER1_STEPS;

  return (
    <div className="flex items-center gap-2">
      {steps.map((label, i) => {
        const n = i + 1;
        const done = n < step;
        const current = n === step;
        return (
          <div key={label} className="flex items-center gap-2">
            <div
              className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-semibold transition-colors ${
                done
                  ? "bg-violet-600 text-white"
                  : current
                  ? "bg-violet-100 text-violet-700 ring-2 ring-violet-400 dark:bg-violet-500/20 dark:text-violet-300"
                  : "bg-gray-100 text-gray-400 dark:bg-gray-800 dark:text-gray-500"
              }`}
            >
              {done ? "✓" : n}
            </div>
            <span className={`hidden text-sm sm:inline ${current ? "font-medium text-gray-800 dark:text-gray-100" : "text-gray-400 dark:text-gray-500"}`}>
              {label}
            </span>
            {n < steps.length && <div className="h-px w-6 bg-gray-200 dark:bg-gray-700" />}
          </div>
        );
      })}
    </div>
  );
}

// The shell around each onboarding step (see pages/onboarding/) — a step indicator, an
// encouraging headline, and a short "why this matters" blurb, wrapping the same form
// component used later from the persistent nav (ResumeIntakePage/JobCriteriaPage/
// SourcesPage, rendered with hideHeader so their own plainer heading doesn't double up
// with this one). First impression for a brand new user, not a permanent page.
export function OnboardingShell({ step, tier, title, blurb, children }: {
  step: number; tier: string; title: string; blurb: string; children: ReactNode;
}) {
  return (
    <div className="mx-auto max-w-2xl space-y-8">
      <StepDots step={step} tier={tier} />
      <div>
        <h1 className="text-2xl font-semibold tracking-tight text-gray-900 dark:text-white">{title}</h1>
        <p className="mt-2 text-sm text-gray-500 dark:text-gray-400">{blurb}</p>
      </div>
      {children}
    </div>
  );
}
