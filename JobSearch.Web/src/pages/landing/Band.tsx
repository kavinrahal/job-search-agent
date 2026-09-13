import type { ReactNode } from "react";
import { cx, styleFor } from "../../ui";

// Full-bleed background band shared by every landing-page section below the hero: the outer
// <section> spans the viewport so its background colour (and hairline, if any) touch both edges,
// while content stays capped at the page's established reading width via the same
// `mx-auto max-w-[1120px] px-6` constraint LandingPage's own header/hero use. One small wrapper
// instead of duplicating that inner-div JSX in every section file.

export type BandTone = "bg" | "shell";

const TONE: Record<BandTone, string> = {
  bg: "bg-bg",
  shell: "bg-shell",
};

export interface BandProps {
  tone?: BandTone;
  hairline?: "t" | "b";
  className?: string;
  /**
   * Full-bleed decorative background (gradient mesh, glow, etc.) rendered on the viewport-wide
   * <section> rather than inside the capped reading column — so it spans both edges like the
   * section's own background does. Clipped to the viewport via the section's `overflow-hidden`,
   * which is why it must live here and not among `children` (that inner div's own overflow would
   * clip it to 1120px).
   */
  bleed?: ReactNode;
  children: ReactNode;
}

export function Band({ tone = "bg", hairline, className, bleed, children }: BandProps) {
  return (
    <section
      className={cx(
        "w-full",
        Boolean(bleed) && "relative overflow-hidden",
        styleFor(TONE, tone),
        hairline === "t" && "hairline-t",
        hairline === "b" && "hairline-b",
      )}
    >
      {bleed}
      <div className={cx("relative mx-auto max-w-[1120px] px-6 py-11", className)}>{children}</div>
    </section>
  );
}
