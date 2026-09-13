import type { ReactNode } from "react";
import { cx } from "./cx";
import { StatusTick, type StatusTickState } from "./StatusTick";

// Application event history: "Interview scheduled, round 2" / "Recruiter screen completed" /
// "Applied", newest first, with a hairline running down through the ticks.
//
// An ordered list, because the order is the meaning. The connecting rule is drawn per item rather
// than as one line behind the column, which is what lets the last item simply not draw it — a rule
// trailing past the final tick into empty space reads as "and then it stopped working".

export function Timeline({ children, className }: { children: ReactNode; className?: string }) {
  return <ol className={cx("m-0 flex list-none flex-col p-0", className)}>{children}</ol>;
}

export interface TimelineItemProps {
  state: StatusTickState;
  title: string;
  /** Where the event came from, e.g. "Detected from an email from talent@example.com". */
  detail?: string;
  /** The date, right-aligned. */
  meta?: ReactNode;
  /** Suppresses the connecting rule and the trailing gap. The final item must set this. */
  last?: boolean;
  className?: string;
}

export function TimelineItem({ state, title, detail, meta, last = false, className }: TimelineItemProps) {
  return (
    <li className={cx("relative grid grid-cols-[15px_1fr_auto] items-start gap-[11px]", !last && "pb-[13px]", className)}>
      {/* data-timeline-connector/-pulse/-dot: stable hooks for the landing page's scroll-driven
          line-draw animation (see pages/landing/TimelineDraw.tsx) to target without coupling this
          shared component to that one placement. Inert here — no behavior or styling depends on
          them outside that one GSAP module. */}
      {!last && (
        <span
          aria-hidden="true"
          data-timeline-connector="true"
          className="absolute top-4 bottom-0 left-[7px] w-px bg-hair"
        >
          {/* The small glowing dot TimelineDraw sends travelling down the connector right after
              it draws in. Opacity 0 at rest so it's invisible whenever that GSAP module isn't
              mounted (the connector's plain end-state, e.g. under reduced motion). */}
          <span
            aria-hidden="true"
            data-timeline-pulse="true"
            className="absolute top-0 left-1/2 h-2 w-2 -translate-x-1/2 rounded-pill bg-brass opacity-0 shadow-[0_0_10px_2px_var(--color-brass)]"
          />
        </span>
      )}
      <span data-timeline-dot="true" className="relative z-1">
        <StatusTick state={state} size="sm" />
      </span>
      <div className="min-w-0">
        <p className="m-0 text-control font-[650] text-ink">{title}</p>
        {detail && <p className="m-0 text-meta text-faint">{detail}</p>}
      </div>
      {meta && <span className="flex-none text-meta whitespace-nowrap text-faint">{meta}</span>}
    </li>
  );
}
