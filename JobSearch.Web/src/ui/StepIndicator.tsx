import { cx } from "./cx";
import { StatusTick } from "./StatusTick";

// The onboarding tick chain: Your background, Job criteria, Sources.
//
// Ticks and hairlines only, no numbered circles and no progress percentage. The design is explicit
// that onboarding gets no nav bar and no tab bar, so this is the only orientation the user has —
// which is why it is three states rather than two: an upcoming step still shows its number, so
// "how many are left" is answerable without counting.
//
// On mobile the labels drop and only the ticks remain, because three labels do not fit and
// truncating them would leave three ambiguous fragments.

export interface Step {
  label: string;
}

export interface StepIndicatorProps {
  steps: Step[];
  /** Zero-based. Everything before it is done, everything after is upcoming. */
  current: number;
  className?: string;
}

export function StepIndicator({ steps, current, className }: StepIndicatorProps) {
  return (
    <ol
      className={cx("m-0 flex list-none items-center gap-1.5 p-0 sm:gap-2", className)}
      aria-label={`Step ${current + 1} of ${steps.length}`}
    >
      {steps.map((step, index) => {
        const state = index < current ? "done" : index === current ? "live" : "pending";
        return (
          <li key={step.label} className="contents">
            <span className="flex items-center gap-1.5">
              <StatusTick state={state} size="lg" number={index + 1} />
              <span
                className={cx(
                  "hidden text-note sm:inline",
                  state === "live" ? "font-bold text-ink" : "text-muted",
                )}
              >
                {step.label}
              </span>
              {/* The visible label is hidden below sm, so the accessible name has to come from
                  somewhere that is never hidden. */}
              <span className="sr-only sm:hidden">{step.label}</span>
            </span>
            {index < steps.length - 1 && <span aria-hidden="true" className="h-px flex-1 bg-hair" />}
          </li>
        );
      })}
    </ol>
  );
}
