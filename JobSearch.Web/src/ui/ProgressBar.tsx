import { cx } from "./cx";

// The thin meter: "Question 5 of 8" on the criteria wizard, and the credit balance.
//
// The fill is scaled, not resized. Animating `width` would relayout the bar on every step;
// scaleX composites on the GPU, which is the rule for everything that moves in this system.

export interface ProgressBarProps {
  value: number;
  max?: number;
  /** Announced by assistive tech, e.g. "Question 5 of 8". Required: a bare meter says nothing. */
  label: string;
  className?: string;
}

export function ProgressBar({ value, max = 100, label, className }: ProgressBarProps) {
  const safeMax = max > 0 ? max : 1;
  const fraction = Math.min(1, Math.max(0, value / safeMax));

  return (
    <div
      role="progressbar"
      aria-label={label}
      aria-valuenow={value}
      aria-valuemin={0}
      aria-valuemax={safeMax}
      className={cx("surface-sunk h-[3px] overflow-hidden rounded-pill", className)}
    >
      <span
        className="block h-full w-full origin-left bg-ember transition-transform duration-500 ease-spring motion-reduce:transition-none"
        style={{ transform: `scaleX(${fraction})` }}
      />
    </div>
  );
}
