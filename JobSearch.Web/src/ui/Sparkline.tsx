import { cx } from "./cx";

// The small bar chart under a StatBlock. Seven-ish bars, the last one in ember.
//
// Emphasising the final bar is the entire point: the reader is not being asked to compare the
// series, only to see where the number they just read sits against its own recent history. No
// axes, no gridlines, no tooltip — anything more and it stops being a sparkline and starts being
// a chart that deserves its own space.

export interface SparklineProps {
  values: number[];
  /** Announced in place of the bars, e.g. "Applications sent, trending up over 7 weeks". */
  label: string;
  className?: string;
}

export function Sparkline({ values, label, className }: SparklineProps) {
  // Scaled against the series' own maximum, not a fixed ceiling, so a flat low series still reads.
  const peak = Math.max(...values, 1);
  const lastIndex = values.length - 1;

  return (
    <div role="img" aria-label={label} className={cx("flex h-[21px] items-end gap-0.5", className)}>
      {values.map((value, index) => (
        <span
          key={index}
          aria-hidden="true"
          className={cx("block flex-1 rounded-t-[2px]", index === lastIndex ? "bg-ember" : "bg-hair-2")}
          // Height is data, not decoration, so it is inline. Floored so a zero still draws a
          // baseline tick rather than vanishing and making the series look shorter than it is.
          style={{ height: `${Math.max(6, (value / peak) * 100)}%` }}
        />
      ))}
    </div>
  );
}
