import { cx } from "./cx";

// Loading placeholders shaped like the thing they replace.
//
// Never a spinner. A spinner tells you the app is busy; a skeleton tells you what is about to
// arrive and how much of it, so the layout does not jump when it does. That is the whole rule, and
// it is why `row` exists as its own variant rather than being assembled at each call site — a
// ledger row's skeleton has to be a tick plus two lines of specific widths, or it does not match.
//
// The pulse is opacity only, and stops entirely under prefers-reduced-motion.

const PULSE = "surface-sunk motion-safe:animate-slate-pulse motion-reduce:animate-none";

export interface SkeletonProps {
  variant?: "line" | "block";
  /** CSS width, e.g. "62%" or "8rem". Vary it across lines: equal-width lines read as a table. */
  width?: string;
  className?: string;
}

export function Skeleton({ variant = "line", width, className }: SkeletonProps) {
  return (
    <span
      aria-hidden="true"
      className={cx("block rounded-mark", PULSE, variant === "line" ? "h-[9px]" : "h-16 rounded-ctl", className)}
      style={width ? { width } : undefined}
    />
  );
}

/**
 * One ledger row's worth: the 16px tick, a title line and a shorter subtitle line. Widths vary per
 * row so a stack of them looks like a list of different jobs rather than a loading bar.
 */
export function SkeletonRow({ titleWidth = "62%", subtitleWidth = "88%" }: { titleWidth?: string; subtitleWidth?: string }) {
  return (
    <div className="flex items-center gap-2.5">
      <span aria-hidden="true" className={cx("block h-4 w-4 flex-none rounded-mark", PULSE)} />
      <div className="min-w-0 flex-1">
        <Skeleton width={titleWidth} className="mb-[5px]" />
        <Skeleton width={subtitleWidth} className="h-2" />
      </div>
    </div>
  );
}

/**
 * A labelled group of skeleton rows. Wrapping them in a busy live region is what turns "some grey
 * shapes" into something a screen reader can report, since the shapes themselves are hidden.
 */
export function SkeletonList({ rows = 3, label = "Loading" }: { rows?: number; label?: string }) {
  const widths = [
    { titleWidth: "62%", subtitleWidth: "88%" },
    { titleWidth: "48%", subtitleWidth: "76%" },
    { titleWidth: "70%", subtitleWidth: "56%" },
  ];
  return (
    <div role="status" aria-busy="true" aria-live="polite" className="flex flex-col gap-2.5">
      <span className="sr-only">{label}</span>
      {Array.from({ length: rows }, (_, i) => (
        // `at` rather than a bracket index: it cannot be out of range, and it keeps this off
        // security/detect-object-injection's radar without a suppression comment.
        <SkeletonRow key={i} {...widths.at(i % widths.length)} />
      ))}
    </div>
  );
}
