import { cx, styleFor } from "./cx";
import { CheckIcon } from "./icons";

// The rounded-square marker that opens every ledger row, timeline entry and onboarding step.
//
// Three states, and they are three genuinely different shapes rather than three colours of the
// same one, so the list is scannable in greyscale:
//   done     filled pos, with a tick
//   live     ember hairline ring around an ember dot, hollow centre
//   pending  hairline ring only, optionally holding a step number
//
// Square-with-5px-corners, never a circle. A circle in a list reads as a bullet; this reads as a
// checkbox, which is what it means.

export type StatusTickState = "done" | "live" | "pending";

export interface StatusTickProps {
  state: StatusTickState;
  /** Shown only in the pending state, for the onboarding step chain. */
  number?: number;
  /** sm inside dense mobile rows, md in ledgers and timelines, lg in the onboarding step chain. */
  size?: "sm" | "md" | "lg";
  className?: string;
}

const BOX = { sm: "h-3.5 w-3.5", md: "h-4 w-4", lg: "h-[19px] w-[19px]" } as const;
const DOT = { sm: "h-1 w-1", md: "h-[5px] w-[5px]", lg: "h-[5px] w-[5px]" } as const;
const TICK = { sm: "h-[7px] w-[7px]", md: "h-2 w-2", lg: "h-2.5 w-2.5" } as const;
const NUMBER = { sm: "text-[8px]", md: "text-[9px]", lg: "text-[9px]" } as const;

export function StatusTick({ state, number, size = "md", className }: StatusTickProps) {
  return (
    <span
      // Decorative: every caller pairs it with a visible label that already carries the meaning,
      // so announcing "done" a second time would just be noise.
      aria-hidden="true"
      className={cx(
        "grid flex-none place-items-center rounded-mark font-bold",
        styleFor(BOX, size),
        styleFor(NUMBER, size),
        state === "done" && "bg-pos text-core",
        state === "live" && "bg-ember-wash shadow-[inset_0_0_0_1.3px_var(--color-ember)]",
        state === "pending" && "text-faint shadow-[inset_0_0_0_1.3px_var(--color-hair-2)]",
        className,
      )}
    >
      {state === "done" && <CheckIcon strokeWidth={2.8} className={styleFor(TICK, size)} />}
      {state === "live" && <span className={cx("block rounded-pill bg-ember", styleFor(DOT, size))} />}
      {state === "pending" && number !== undefined && <span>{number}</span>}
    </span>
  );
}
