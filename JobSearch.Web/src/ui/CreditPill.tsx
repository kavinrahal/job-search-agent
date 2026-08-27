import { cx } from "./cx";

// Locale-grouped, same reasoning as CountUp: a four-figure balance is unreadable ungrouped.
const FORMAT = new Intl.NumberFormat();

// The credit balance in the top bar: "128 credits".
//
// Quiet by design. It is a number the user needs available, not a call to action, so it sits at
// ink-2 on a shell pill rather than in the ember the rest of the bar reserves for actions. It only
// changes colour when the balance is actually a problem.

// At or below this the pill goes ember, because at that point it *is* a call to action. A
// constant rather than a prop: it is one product decision, and a per-call-site threshold would let
// the same balance read as urgent on one screen and fine on the next.
const LOW_BALANCE = 5;

export interface CreditPillProps {
  credits: number;
  /** Drops the word "credits", for the mobile bar where only the number fits. */
  compact?: boolean;
  className?: string;
}

export function CreditPill({ credits, compact = false, className }: CreditPillProps) {
  const low = credits <= LOW_BALANCE;
  return (
    <span
      className={cx(
        "inline-flex items-center gap-1.5 rounded-pill px-2.5 py-[3px] text-caption whitespace-nowrap",
        low ? "bg-ember-wash text-ember" : "hairline-ring bg-shell text-ink-2",
        className,
      )}
    >
      <b className={cx("font-bold tabular-nums", low ? "text-ember" : "text-ink")}>{FORMAT.format(credits)}</b>
      {/* Always present for assistive tech even when visually dropped: "128" alone is not a fact. */}
      <span className={cx(compact && "sr-only")}>credits</span>
    </span>
  );
}
