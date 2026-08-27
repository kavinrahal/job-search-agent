import type { ReactNode } from "react";
import { cx } from "./cx";

// "Why this one." The single sentence explaining a match, in a sunk well with a coloured rule
// down its left edge.
//
// Two variants, and the difference is the point of the component: a pos rule means the system is
// recommending this, a faint rule means it found it but is telling you why it held back. Same
// shape, same position, opposite meaning — so the reader learns one place to look.

export type MatchReasonTone = "why" | "held-back";

export interface MatchReasonProps {
  tone?: MatchReasonTone;
  /** The bolded opener, e.g. "Why this one." or "Held back." */
  heading: string;
  children: ReactNode;
  className?: string;
}

export function MatchReason({ tone = "why", heading, children, className }: MatchReasonProps) {
  return (
    <p
      className={cx(
        "m-0 rounded-ctl bg-sunk px-2.5 py-2 text-caption text-ink-2",
        tone === "why"
          ? "shadow-[inset_2px_0_0_var(--color-pos)]"
          : "shadow-[inset_2px_0_0_var(--color-faint)]",
        className,
      )}
    >
      <b className="font-[650] text-ink">{heading}</b> {children}
    </p>
  );
}
