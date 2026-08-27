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
      {!last && <span aria-hidden="true" className="absolute top-4 bottom-0 left-[7px] w-px bg-hair" />}
      <StatusTick state={state} size="sm" className="relative z-1" />
      <div className="min-w-0">
        <p className="m-0 text-control font-[650] text-ink">{title}</p>
        {detail && <p className="m-0 text-meta text-faint">{detail}</p>}
      </div>
      {meta && <span className="flex-none text-meta whitespace-nowrap text-faint">{meta}</span>}
    </li>
  );
}
