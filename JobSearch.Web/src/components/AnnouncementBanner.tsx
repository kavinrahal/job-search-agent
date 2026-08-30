import { useState } from "react";
import { CloseIcon, WarningIcon, cx } from "../ui";

// Keyed by the message text itself, not a plain dismissed boolean — the requirement (see
// SiteStatus/Emergency page) is that a *new* announcement the admin sets always shows even if
// an older, different message was already dismissed. A single "dismissed: true" flag would
// permanently suppress every future banner the moment one was ever closed.
const STORAGE_PREFIX = "ws-banner-dismissed:";

function isDismissed(message: string): boolean {
  try {
    return localStorage.getItem(STORAGE_PREFIX + message) === "1";
  } catch {
    return false;
  }
}

function dismiss(message: string): void {
  try {
    localStorage.setItem(STORAGE_PREFIX + message, "1");
  } catch {
    // Private browsing / storage disabled — the banner just reappears next visit, not an error.
  }
}

// The dismissible strip shown above the normal app content when only bannerActive is true (not
// maintenanceMode) — the app stays fully usable underneath. Wraps whichever branch (AuthedApp
// or LoggedOutRoutes) is actually rendering, in App.tsx.
export function AnnouncementBanner({ message }: { message: string }) {
  const [dismissed, setDismissed] = useState(() => isDismissed(message));
  if (dismissed) return null;

  return (
    <div
      role="status"
      className={cx(
        "flex items-center justify-center gap-2.5 bg-brass-wash px-4 py-2 text-center text-note text-ink-2",
        "shadow-[inset_0_-1px_0_0_color-mix(in_srgb,var(--color-brass)_26%,transparent)]",
      )}
    >
      <WarningIcon className="h-3.5 w-3.5 flex-none text-brass" aria-hidden="true" />
      <span className="min-w-0">{message}</span>
      <button
        type="button"
        aria-label="Dismiss announcement"
        onClick={() => {
          dismiss(message);
          setDismissed(true);
        }}
        className="ml-1 grid h-5 w-5 flex-none place-items-center rounded-ctl text-muted transition-colors hover:bg-black/5 hover:text-ink focus-ring"
      >
        <CloseIcon className="h-3 w-3" />
      </button>
    </div>
  );
}
