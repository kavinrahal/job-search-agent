import type { ReactNode } from "react";
import { cx } from "./cx";

// Page title plus its one-line tagline, with room for a trailing action.
//
// Supersedes components/PageTagline.tsx, which was the tagline without the title — every caller
// then wrote its own heading next to it, so the two drifted apart page by page. Keeping them in one
// component is what stops that.

export interface PageHeaderProps {
  title: string;
  /** One line. If it needs two, it is not a tagline, it is body copy and belongs on the page. */
  tagline?: string;
  /** Buttons or filters aligned to the right of the title. */
  actions?: ReactNode;
  className?: string;
}

export function PageHeader({ title, tagline, actions, className }: PageHeaderProps) {
  return (
    <div className={cx("mb-3.5 flex flex-wrap items-start justify-between gap-3", className)}>
      <div className="min-w-0">
        <h1 className="m-0 text-display font-bold text-balance text-ink">{title}</h1>
        {tagline && <p className="m-0 text-caption text-pretty text-faint">{tagline}</p>}
      </div>
      {actions && <div className="flex flex-none items-center gap-2">{actions}</div>}
    </div>
  );
}
