import type { ReactNode } from "react";
import { cx } from "./cx";

// The A4 preview frame for a generated CV or cover letter.
//
// Two rules, and everything here exists to enforce them:
//
// 1. The page never reflows. It is a picture of a printed document, so its proportions are fixed
//    at 210/297 and its content does not rewrap to fit the viewport. A preview that reflows is
//    lying about what will come out of the printer.
// 2. The page never shrinks below MIN_WIDTH. Below that the 7.5px body text stops being legible
//    and the preview becomes decoration. The container scrolls horizontally instead.
//
// The page itself stays white and dark-inked in both themes, matching the existing
// ResumePreviewPane and ResumePdfViewer convention: paper is not a surface in the design system,
// it is the artefact being previewed, and theming it would misrepresent the output.

// Below this the page scrolls rather than shrinking: 320px is where the 7.5px body type stops
// being legible. A constant, not a prop, because it is a property of the type size rather than of
// any particular screen.
const MIN_WIDTH = 320;

export interface DocumentPageProps {
  children: ReactNode;
  /**
   * Caps the page on a wide screen. The design uses two: the Generate preview fills its column,
   * the resume builder's holds it to 330px beside the editor.
   */
  maxWidth?: number;
  className?: string;
}

export function DocumentPage({ children, maxWidth = 420, className }: DocumentPageProps) {
  return (
    // The scroller, and the reason it is a separate element: overflow-x-auto has to sit on a box
    // that is allowed to be narrower than its content. Putting it on the sunk mat would clip the
    // mat's own padding instead.
    <div className={cx("w-full overflow-x-auto", className)}>
      <div className="surface-shell-e1 inline-block min-w-full">
        {/* The sunk mat the page sits on — the same relationship as a print proof on a desk. */}
        <div className="rounded-core bg-sunk p-1.5">
          <div
            className="mx-auto overflow-hidden rounded-ctl bg-white text-[#1a1e24] shadow-e1"
            style={{ aspectRatio: "210 / 297", minWidth: MIN_WIDTH, maxWidth, width: "100%" }}
          >
            {children}
          </div>
        </div>
      </div>
    </div>
  );
}
