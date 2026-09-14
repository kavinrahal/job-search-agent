import { useState } from "react";
import { cx } from "../ui";

// A screenshot slot for illustrated step-by-step help pages (currently just the Gmail forwarding
// walkthrough — see GmailForwardingHelpPage). `src` points at a fixed path under public/images/help/
// that may or may not have a real file yet. The <img> is always attempted; if it 404s, onError
// swaps in a labeled placeholder box instead of the browser's broken-image icon, so an unfilled
// slot reads as "screenshot coming soon" rather than as an error. The moment a real file lands at
// that exact path, this same component renders it — no code change required.
export interface ScreenshotSlotProps {
  /** Fixed public/ path, e.g. "/images/help/gmail-forwarding-step-1.png". */
  src: string;
  alt: string;
  /** Shown inside the placeholder box, e.g. "Step 1 — Gmail Settings menu with See all settings". */
  placeholderLabel: string;
  className?: string;
}

export function ScreenshotSlot({ src, alt, placeholderLabel, className }: ScreenshotSlotProps) {
  const [failed, setFailed] = useState(false);

  if (failed) {
    return (
      <div
        className={cx(
          "surface-sunk flex min-h-[160px] flex-col items-center justify-center gap-2 rounded-ctl px-4 py-8 text-center",
          className,
        )}
      >
        <svg
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth={1.5}
          strokeLinecap="round"
          strokeLinejoin="round"
          aria-hidden="true"
          focusable="false"
          className="h-6 w-6 text-faint"
        >
          <rect x="3" y="4" width="18" height="16" rx="2" />
          <circle cx="8.5" cy="10" r="1.5" />
          <path d="m21 16-5-5-9 9" />
        </svg>
        <p className="m-0 text-caption text-faint">Screenshot: {placeholderLabel}</p>
      </div>
    );
  }

  return (
    <img
      src={src}
      alt={alt}
      onError={() => setFailed(true)}
      className={cx("block w-full rounded-ctl object-cover", className)}
    />
  );
}
