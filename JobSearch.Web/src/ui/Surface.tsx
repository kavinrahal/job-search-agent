import type { ReactNode } from "react";
import { cx, styleFor } from "./cx";

// The double bezel. This is the system's signature and the component everything else sits inside.
//
// An outer *shell* (shell background, 6px padding, 20px radius, hairline ring) wrapping an inner
// *core* (core background, 14px radius, inset top highlight). 20 minus the 6px padding is 14, so
// the two curves are concentric — that is the whole trick, and it is why the radii are locked and
// the padding is not a prop.

export type SurfaceElevation = "flat" | "raised" | "floating";

const SHELL: Record<SurfaceElevation, string> = {
  // Sits directly on the ground with no drop shadow. For surfaces already inside another surface,
  // where a second shadow would read as a rendering artefact rather than as depth.
  flat: "surface-shell",
  raised: "surface-shell-e1",
  // Only for things that genuinely float above the page: drawers, sheets, menus.
  floating: "surface-shell-e2",
};

const PADDING = {
  none: "",
  sm: "p-3",
  md: "p-3.5",
  lg: "p-4",
} as const;

export interface SurfaceProps {
  children: ReactNode;
  elevation?: SurfaceElevation;
  /** Padding *inside* the core. The 6px shell padding is fixed — it is what makes the radii line up. */
  padding?: keyof typeof PADDING;
  /**
   * Clip the core's children to its 14px radius. Needed whenever a child paints to the core's
   * edge (a ledger's hover fill, a full-bleed footer strip); off by default because clipping also
   * traps focus outlines and any popover a child wants to open.
   */
  clip?: boolean;
  className?: string;
}

export function Surface({ children, elevation = "raised", padding = "md", clip = false, className }: SurfaceProps) {
  return (
    <div className={cx(styleFor(SHELL, elevation), className)}>
      <div className={cx("surface-core", styleFor(PADDING, padding), clip && "overflow-hidden")}>{children}</div>
    </div>
  );
}

/**
 * A recessed well: the sunk background plus a hairline ring, at control radius. Inputs, segmented
 * tracks and the match-reason block are all this shape. Exported because several composites need
 * the same treatment on an element they own themselves.
 */
export function Well({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={cx("surface-sunk rounded-ctl", className)}>{children}</div>;
}
