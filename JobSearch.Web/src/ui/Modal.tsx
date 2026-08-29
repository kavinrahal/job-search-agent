import { useCallback, useEffect, useRef, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { cx } from "./cx";
import { IconButton } from "./Button";
import { CloseIcon } from "./icons";

// A centred modal dialog: a fixed-width card floating in the middle of the screen, scrim behind.
//
// Sibling to Drawer, and it shares Drawer's modal contract wholesale — the two differ only in
// chrome. Use this when the content is a self-contained card the reader steps into and back out of
// (a breakdown, a confirmation); use Drawer when it's an edge-anchored panel or a mobile sheet.
//
// The contract, identical to Drawer's and not optional:
//   - Escape closes.
//   - Focus moves in on open and returns to the trigger on close.
//   - Tab is trapped inside while it is open.
//   - The background does not scroll.
//   - Clicking the scrim closes it.

const FOCUSABLE =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

export interface ModalProps {
  open: boolean;
  onClose: () => void;
  /** Also the panel's accessible name. */
  title: string;
  description?: string;
  children: ReactNode;
  /** Pinned to the bottom of the panel, outside the scrolling body. */
  footer?: ReactNode;
  className?: string;
}

export function Modal({ open, onClose, title, description, children, footer, className }: ModalProps) {
  const panelRef = useRef<HTMLDivElement>(null);
  const restoreFocusTo = useRef<HTMLElement | null>(null);

  const focusable = useCallback(
    () => Array.from(panelRef.current?.querySelectorAll<HTMLElement>(FOCUSABLE) ?? []).filter(el => el.offsetParent !== null),
    [],
  );

  // Move focus in, and remember where it came from so it can go back. Without the restore, closing
  // a modal opened from the twelfth row of a list dumps the keyboard user back at the top of the
  // document.
  useEffect(() => {
    if (!open) return;
    restoreFocusTo.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const first = focusable().at(0) ?? panelRef.current;
    first?.focus();
    return () => restoreFocusTo.current?.focus();
  }, [open, focusable]);

  // Lock the background. Restoring the previous value rather than clearing it means two stacked
  // overlays cannot leave the page permanently unscrollable.
  useEffect(() => {
    if (!open) return;
    const previous = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.body.style.overflow = previous;
    };
  }, [open]);

  useEffect(() => {
    if (!open) return;
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") {
        e.stopPropagation();
        onClose();
        return;
      }
      if (e.key !== "Tab") return;

      const items = focusable();
      if (items.length === 0) {
        e.preventDefault();
        return;
      }
      const first = items.at(0);
      const last = items.at(-1);
      // Wrap at both ends. Also catches the case where focus has somehow escaped the panel
      // entirely, by pulling it back to the first item.
      if (e.shiftKey && (document.activeElement === first || !panelRef.current?.contains(document.activeElement))) {
        e.preventDefault();
        last?.focus();
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault();
        first?.focus();
      }
    }
    document.addEventListener("keydown", onKeyDown, true);
    return () => document.removeEventListener("keydown", onKeyDown, true);
  }, [open, onClose, focusable]);

  if (!open) return null;

  // Portalled to <body> rather than rendered in place. A z-index only competes inside its own
  // stacking context, so any ancestor with a transform, filter, backdrop-blur or its own z-index
  // silently caps this beneath everything outside it — AppShell's <main> is exactly such an
  // ancestor (the bug Drawer's own note documents). A portal takes the panel out of that hierarchy
  // entirely, which also stops any `overflow: hidden` ancestor from clipping it.
  return createPortal(
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      {/* Scrim. A plain div rather than a button: it is decorative, and the panel's own close
          button plus Escape already give every user a way out. */}
      <div aria-hidden="true" onClick={onClose} className="absolute inset-0 bg-[rgba(10,13,17,.5)]" />

      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-label={title}
        tabIndex={-1}
        className={cx(
          "surface-shell-e2 relative flex max-h-[85vh] w-full max-w-[480px] flex-col overscroll-contain focus-ring",
          className,
        )}
      >
        <div className="surface-core flex min-h-0 flex-1 flex-col overflow-hidden">
          <div className="flex items-start justify-between gap-3 px-3.5 pt-3 pb-2">
            <div className="min-w-0">
              <h2 className="m-0 text-heading font-bold text-balance text-ink">{title}</h2>
              {description && <p className="m-0 text-caption text-muted">{description}</p>}
            </div>
            <IconButton aria-label="Close" size="sm" onClick={onClose}>
              <CloseIcon className="h-4 w-4" />
            </IconButton>
          </div>

          <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain px-3.5 pb-3.5 slate-scroll">{children}</div>

          {footer && <div className="hairline-t flex flex-col gap-2 px-3.5 py-3">{footer}</div>}
        </div>
      </div>
    </div>,
    document.body,
  );
}
