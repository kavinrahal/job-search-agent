import { useEffect, useRef, useState } from "react";
import { cx } from "./cx";

// A number that counts up once, the first time it scrolls into view.
//
// Once, not on every scroll past — a stat that re-animates every time you come back to it stops
// being information and becomes a toy. And only on first view, because animating a number the
// reader has not looked at yet wastes the effect.
//
// Under prefers-reduced-motion it renders the final value immediately with no rAF loop at all.
// This is not a shortened animation, it is no animation: the whole point of the setting is that
// motion is unwelcome, not that it should be faster.

const DURATION_MS = 950;

// Grouped by the reader's own locale rather than with a hardcoded comma: "1,284" and "1.284" are
// both correct depending on where you are, and an ungrouped "1284" is harder to read than either.
// Constructed once at module scope, since building a formatter per frame during the count-up would
// be the most expensive thing in the animation.
const FORMAT = new Intl.NumberFormat();

export interface CountUpProps {
  value: number;
  className?: string;
}

function prefersReducedMotion(): boolean {
  return typeof matchMedia === "function" && matchMedia("(prefers-reduced-motion: reduce)").matches;
}

export function CountUp({ value, className }: CountUpProps) {
  const reduced = prefersReducedMotion();
  const canObserve = typeof IntersectionObserver === "function";
  // Start at the final value whenever there will be no animation, so the number is never
  // momentarily wrong for a reader who will not see it move.
  const [display, setDisplay] = useState(() => (reduced || !canObserve ? value : 0));
  const ref = useRef<HTMLSpanElement>(null);
  const hasRun = useRef(reduced || !canObserve);

  useEffect(() => {
    if (hasRun.current) {
      setDisplay(value);
      return;
    }
    const el = ref.current;
    if (!el) return;

    let frame = 0;
    const observer = new IntersectionObserver(
      entries => {
        for (const entry of entries) {
          if (!entry.isIntersecting || hasRun.current) continue;
          hasRun.current = true;
          observer.disconnect();

          const start = performance.now();
          const step = (now: number) => {
            const progress = Math.min(1, (now - start) / DURATION_MS);
            // Cubic ease-out: fast off the mark, settling rather than stopping. Matches the
            // spring easing everything else in the system moves with.
            const eased = 1 - Math.pow(1 - progress, 3);
            setDisplay(Math.round(value * eased));
            if (progress < 1) frame = requestAnimationFrame(step);
          };
          frame = requestAnimationFrame(step);
        }
      },
      { threshold: 0.4 },
    );

    observer.observe(el);
    return () => {
      observer.disconnect();
      cancelAnimationFrame(frame);
    };
  }, [value]);

  return (
    <span ref={ref} className={cx("tabular-nums", className)}>
      {/* The intermediate values are meaningless to a screen reader, and worse, one that reads
          mid-animation would announce a wrong number. It gets the real one, once. */}
      <span aria-hidden="true">{FORMAT.format(display)}</span>
      <span className="sr-only">{FORMAT.format(value)}</span>
    </span>
  );
}
