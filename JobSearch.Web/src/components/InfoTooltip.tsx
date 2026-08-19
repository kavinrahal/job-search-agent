import { useState } from "react";

// Click/tap-to-toggle, not hover-only — hover doesn't work reliably on touch, and this way
// mouse and touch behave identically. Positioning is deliberately overflow-safe: left-0
// (never extends left of its trigger, so it can't go negative off a narrow screen) plus a
// max-width clamped to the viewport (so a trigger near the right edge still can't push the
// popover past it) — the exact class of bug this session's grid-overflow fixes were about.
export function InfoTooltip({ text }: { text: string }) {
  const [open, setOpen] = useState(false);

  return (
    <span className="relative inline-block">
      <button
        type="button"
        onClick={() => setOpen(o => !o)}
        onBlur={() => setOpen(false)}
        aria-label="More info"
        className="ml-1 inline-flex h-4 w-4 items-center justify-center rounded-full bg-gray-200 text-[10px] font-bold text-gray-500 hover:bg-gray-300"
      >
        ?
      </button>
      {open && (
        <span className="absolute left-0 top-full z-20 mt-1 w-64 max-w-[calc(100vw-2.5rem)] rounded-lg border border-gray-200 bg-white p-2 text-xs text-gray-600 shadow-lg">
          {text}
        </span>
      )}
    </span>
  );
}
