import type { ReactNode } from "react";
import { cx, styleFor } from "./cx";

// The status mark: Strong, Good, Weak, Interviewing, Offer.
//
// A 5px rounded mark, not a pill — the shape is doing work here. Chips are pills because you press
// them; a badge is a read-only fact about a row, and giving it the same shape as a chip would
// invite people to click it. Uppercase and letter-spaced so it reads as a stamp at 10px.

export type BadgeVariant = "strong" | "good" | "weak" | "live" | "neutral";

const VARIANT: Record<BadgeVariant, string> = {
  strong: "bg-pos-wash text-pos",
  good: "bg-brass-wash text-brass",
  weak: "bg-sunk text-faint",
  live: "bg-ember-wash text-ember",
  neutral: "bg-shell text-muted",
};

export function Badge({ children, variant = "neutral", className }: { children: ReactNode; variant?: BadgeVariant; className?: string }) {
  return (
    <span
      className={cx(
        "inline-block rounded-mark px-2 py-[3px] text-eyebrow font-bold tracking-[.06em] whitespace-nowrap uppercase",
        styleFor(VARIANT, variant),
        className,
      )}
    >
      {children}
    </span>
  );
}
