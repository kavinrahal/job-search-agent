import type { ReactNode } from "react";
import { cx, styleFor } from "./cx";

// The nothing-here state.
//
// The design's rule for the copy, worth restating because it is the part that gets written wrong:
// say what happens next, do not report the absence. "Nothing delivered yet. The first run happens
// tonight, anything matching your criteria will be here in the morning" — not "No results found".
// The user can already see there is nothing there.

export type EmptyStateTone = "neutral" | "positive" | "ember";

const TONE: Record<EmptyStateTone, string> = {
  neutral: "hairline-ring bg-sunk text-faint",
  positive: "bg-pos-wash text-pos",
  ember: "bg-ember-wash text-ember",
};

export interface EmptyStateProps {
  icon: ReactNode;
  tone?: EmptyStateTone;
  title: string;
  body: string;
  /** A single Button. Two competing actions in an empty state means neither is the obvious next step. */
  action?: ReactNode;
  className?: string;
}

export function EmptyState({ icon, tone = "neutral", title, body, action, className }: EmptyStateProps) {
  return (
    <div className={cx("flex flex-col items-center gap-2.5 px-4 py-6 text-center", className)}>
      <span aria-hidden="true" className={cx("grid h-[42px] w-[42px] place-items-center rounded-core", styleFor(TONE, tone), "[&>svg]:h-[19px] [&>svg]:w-[19px]")}>
        {icon}
      </span>
      <h3 className="m-0 text-lede font-bold text-balance text-ink">{title}</h3>
      <p className="m-0 max-w-[34ch] text-note text-pretty text-muted">{body}</p>
      {action}
    </div>
  );
}
