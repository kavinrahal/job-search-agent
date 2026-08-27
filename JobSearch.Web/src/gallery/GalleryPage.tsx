import { useState } from "react";
import { Grain, SegmentedControl, ThemeProvider, ThemeToggle, styleFor } from "../ui";
import { GalleryPrimitives } from "./GalleryPrimitives";
import { GalleryComposites } from "./GalleryComposites";

// The design system gallery. Development only — see main.tsx for how it is kept out of the
// production bundle entirely rather than merely hidden.
//
// Both themes render side by side rather than behind a toggle, because most of what goes wrong in
// a two-theme system goes wrong in only one of them, and a toggle means seeing one at a time. The
// width control narrows both panes together so the mobile behaviour of every component (ledger
// truncation, segmented control scrolling, A4 page scrolling) is reviewable without a device.

type Width = "desktop" | "mobile";

const PANE_WIDTH: Record<Width, string> = {
  desktop: "max-w-none",
  // 390px is an iPhone 15's CSS width, and the narrowest thing worth designing for here.
  mobile: "max-w-[390px]",
};

function GalleryContent() {
  return (
    <>
      <GalleryPrimitives />
      <GalleryComposites />
    </>
  );
}

function Pane({ theme, width }: { theme: "light" | "dark"; width: Width }) {
  return (
    <div className={`slate-theme-${theme} min-w-0 flex-1`}>
      <div className="hairline-b sticky top-[57px] z-20 bg-bg/90 px-4 py-2 backdrop-blur-sm">
        <span className="text-eyebrow tracking-[.2em] text-faint uppercase">{theme}</span>
      </div>
      <div className={`bg-bg px-4 pb-24 text-ink ${styleFor(PANE_WIDTH, width)}`}>
        <GalleryContent />
      </div>
    </div>
  );
}

export function GalleryPage() {
  const [width, setWidth] = useState<Width>("desktop");

  return (
    // The gallery's own chrome follows the document theme, so the ThemeToggle in the bar is doing
    // something visible even though the two panes below are pinned.
    <ThemeProvider>
      <div className="min-h-screen bg-bg text-ink">
        <Grain />
        <header className="hairline-b sticky top-0 z-30 flex flex-wrap items-center justify-between gap-3 bg-bg/90 px-4 py-2.5 backdrop-blur-md">
          <div>
            <p className="m-0 text-body font-bold text-ink">
              Slate design system <span className="font-normal text-muted">/ gallery</span>
            </p>
            <p className="m-0 text-meta text-faint">Development only. Not built into production.</p>
          </div>
          <div className="flex items-center gap-2.5">
            <SegmentedControl
              label="Preview width"
              value={width}
              onChange={setWidth}
              segments={[
                { value: "desktop", label: "Desktop" },
                { value: "mobile", label: "Mobile" },
              ]}
            />
            <ThemeToggle />
          </div>
        </header>

        <div className="relative z-1 flex flex-col lg:flex-row lg:divide-x lg:divide-hair">
          <Pane theme="light" width={width} />
          <Pane theme="dark" width={width} />
        </div>
      </div>
    </ThemeProvider>
  );
}
