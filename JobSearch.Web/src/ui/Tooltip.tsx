import { useEffect, useId, useRef, useState } from "react";
import { cx } from "./cx";

// The "?" affordance next to a label. Supersedes components/InfoTooltip.tsx.
//
// Kept from InfoTooltip, because it was right: click/tap to toggle rather than hover-only (hover
// does not exist on touch, and a hover-only tooltip is simply invisible to half the users), and
// overflow-safe positioning — anchored left-0 so it can never extend past the left edge, with a
// max-width clamped to the viewport so a trigger near the right edge cannot push it past that one.
//
// Added on top of that: hover as well as click for mouse users, Escape to dismiss, and
// aria-describedby so the text is actually associated with the trigger instead of being an
// unlabelled span that appears nearby.

export interface TooltipProps {
  text: string;
  /** Overrides the default "More info" trigger label. Name the field when there are several. */
  label?: string;
  className?: string;
}

export function Tooltip({ text, label = "More info", className }: TooltipProps) {
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLSpanElement>(null);
  const id = useId();

  useEffect(() => {
    if (!open) return;
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") setOpen(false);
    }
    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, [open]);

  return (
    <span
      ref={rootRef}
      className={cx("relative inline-block", className)}
      onMouseEnter={() => setOpen(true)}
      onMouseLeave={() => setOpen(false)}
    >
      <button
        type="button"
        aria-label={label}
        aria-expanded={open}
        aria-describedby={open ? id : undefined}
        onClick={() => setOpen(o => !o)}
        onFocus={() => setOpen(true)}
        onBlur={() => setOpen(false)}
        className={cx(
          "ml-1 inline-grid h-4 w-4 place-items-center rounded-pill bg-sunk text-[10px] font-bold text-muted focus-ring tappable",
          "transition-[background-color,color,transform] duration-300 ease-spring motion-reduce:transition-none",
          "hover:bg-shell hover:text-ink active:scale-[.92]",
        )}
      >
        ?
      </button>
      {open && (
        <span
          id={id}
          role="tooltip"
          className="surface-shell-e2 absolute top-full left-0 z-50 mt-1 block w-64 max-w-[calc(100vw-2.5rem)]"
        >
          <span className="surface-core block px-2.5 py-2 text-caption text-pretty text-ink-2">{text}</span>
        </span>
      )}
    </span>
  );
}
