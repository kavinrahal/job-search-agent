import type { ReactNode } from "react";
import { cx } from "./cx";

// Two micro labels that look similar and mean different things.
//
// Eyebrow is quiet structural labelling: "While you were asleep", "Timeline", "Arrangement". It
// sits above the thing it names and recedes.
//
// Kicker is a badge that announces: "Handled overnight", "Slate, complete set". Ember on an ember
// wash, pill-shaped, used once per screen at most. Using two Kickers on one page is the fastest
// way to make neither of them land.

export function Eyebrow({ children, className }: { children: ReactNode; className?: string }) {
  return <p className={cx("m-0 text-eyebrow text-faint uppercase", className)}>{children}</p>;
}

export function Kicker({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <span className={cx("inline-block rounded-pill bg-ember-wash px-3 py-[5px] text-eyebrow text-ember uppercase", className)}>
      {children}
    </span>
  );
}
