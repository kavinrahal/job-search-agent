import type { ReactNode } from "react";
import { cx, styleFor } from "./cx";
import { DangerIcon, InfoIcon, WarningIcon } from "./icons";

// The banded notice. Its main job in this product is the accuracy warning on Generate: "Worth
// checking before you send. The phrase 'led a team of six' does not appear anywhere in your
// background."
//
// Brass, not red, for that one. Red would say the generation failed. Brass says the output is fine
// but you are about to put your name on a claim the system cannot substantiate, which is a
// different and more useful thing to say.

export type CalloutVariant = "warning" | "info" | "danger";

const VARIANT: Record<CalloutVariant, { box: string; icon: string; Icon: typeof WarningIcon }> = {
  warning: {
    box: "bg-brass-wash shadow-[inset_0_0_0_1px_color-mix(in_srgb,var(--color-brass)_26%,transparent)]",
    icon: "text-brass",
    Icon: WarningIcon,
  },
  // No dedicated info wash exists in the palette, and inventing one would add a sixth accent to a
  // system built on three. A sunk well with a hairline reads as neutral, which is what info means.
  info: { box: "surface-sunk", icon: "text-muted", Icon: InfoIcon },
  danger: {
    box: "bg-ember-wash shadow-[inset_0_0_0_1px_color-mix(in_srgb,var(--color-ember)_30%,transparent)]",
    icon: "text-ember",
    Icon: DangerIcon,
  },
};

export interface CalloutProps {
  variant?: CalloutVariant;
  title: string;
  children?: ReactNode;
  className?: string;
}

export function Callout({ variant = "warning", title, children, className }: CalloutProps) {
  const { box, icon, Icon } = styleFor(VARIANT, variant);
  return (
    <div className={cx("flex items-start gap-2.5 rounded-ctl px-3 py-2.5 text-control text-ink-2", box, className)}>
      <Icon className={cx("mt-0.5 h-3.5 w-3.5 flex-none", icon)} />
      <div className="min-w-0">
        <b className="font-[650] text-ink">{title}</b>
        {children && <span> {children}</span>}
      </div>
    </div>
  );
}
