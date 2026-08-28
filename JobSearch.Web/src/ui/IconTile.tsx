import type { ReactNode } from "react";
import { cx } from "./cx";

// Small square "wash" icon tile — the ember-wash tile behind a single-color outline icon, used
// to front short answer/feature cards (Help's FAQ grid, etc). Same wash/ink pairing as Badge's
// "live" variant (bg-ember-wash text-ember) and the same 1.5 stroke every icon in ./icons.tsx
// uses — this isn't one of those icons (Help's four questions need marks that don't exist in
// that set: a clock, an envelope) so it stays a raw inline svg rather than extending the shared
// icon set for two one-off glyphs. Pass raw <path>/<circle> children (viewBox fixed at 0 0 24 24,
// matching ./icons.tsx) rather than a whole <svg>, so callers don't repeat the stroke/viewBox
// boilerplate each time. Radius is the system's --radius-ctl (9px) — the shape lock in
// tokens.css: nothing in src/ui may invent a fifth radius.
export function IconTile({ children, size = 30, className }: { children: ReactNode; size?: number; className?: string }) {
  return (
    <div
      className={cx("mb-2 grid shrink-0 place-items-center rounded-ctl bg-ember-wash text-ember", className)}
      style={{ width: size, height: size }}
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
        className="h-[15px] w-[15px]"
      >
        {children}
      </svg>
    </div>
  );
}
