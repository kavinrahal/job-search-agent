import { cx } from "./cx";
import { CountUp } from "./CountUp";
import { Sparkline } from "./Sparkline";

// Big number, label, optional sparkline.
//
// Renders unboxed — no card, no border, no background. The density rules ban a box around a
// metric, and for a good reason: four boxed metrics in a row read as four separate things
// competing, where four bare ones read as one row of figures. Put them inside a single Surface if
// they need containing, and separate them with a hairline (see the gallery's metrics pair).

export interface StatBlockProps {
  value: number;
  label: string;
  /** Rendered immediately after the number, e.g. "%". Kept out of `value` so CountUp still animates. */
  suffix?: string;
  /** Seven-ish points. Omit it when there is no history worth showing rather than passing a flat line. */
  trend?: number[];
  className?: string;
}

export function StatBlock({ value, label, suffix, trend, className }: StatBlockProps) {
  return (
    <div className={cx("min-w-0", className)}>
      <p className="m-0 text-stat font-bold text-ink">
        <CountUp value={value} />
        {suffix}
      </p>
      <p className="mt-px mb-0 text-caption text-muted">{label}</p>
      {trend && <Sparkline values={trend} label={`${label}, recent trend`} className="mt-2" />}
    </div>
  );
}
