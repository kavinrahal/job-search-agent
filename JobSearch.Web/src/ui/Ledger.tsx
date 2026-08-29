import type { ReactNode } from "react";
import { cx } from "./cx";
import { StatusTick, type StatusTickState } from "./StatusTick";

// The grouped scan list. Today's "Worth a look", Discover's results, the whole Applications page.
//
// Three columns: a status tick, the main pair (company over role), and meta on the right. The
// design deliberately has no card per row — rows are separated by a hairline and nothing else, so
// twelve of them read as one list you can scan rather than twelve objects you have to parse.

export function Ledger({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={cx("flex flex-col", className)}>{children}</div>;
}

/** A heading inside the list: "This week", "Earlier". Not a section, just a marker in the flow. */
export function LedgerGroup({ children, className }: { children: ReactNode; className?: string }) {
  return <p className={cx("m-0 px-3.5 pt-3 pb-[5px] text-eyebrow text-faint uppercase", className)}>{children}</p>;
}

export interface LedgerRowProps {
  /** The generic done/live/pending mark. Ignored when `tickIcon` is given. */
  tick?: StatusTickState;
  /** A caller-supplied glyph for the leading slot instead of the generic tick — e.g. a
   * status-specific icon where the state itself has its own visual identity beyond done/live/pending. */
  tickIcon?: ReactNode;
  /** The strong line: a company, an employer. */
  title: string;
  /** The quiet line: a role title. Truncates to one line rather than wrapping. */
  subtitle?: string;
  /** Badges, dates. Never shrinks — it is the fixed edge the truncation is measured against. */
  meta?: ReactNode;
  href?: string;
  onClick?: () => void;
  className?: string;
}

export function LedgerRow({ tick, tickIcon, title, subtitle, meta, href, onClick, className }: LedgerRowProps) {
  const classes = cx(
    // `[[data-ledger-row]+&]` gives the hairline only to a row that directly follows another row,
    // so the first row under a group heading does not get a stray rule above it.
    "grid grid-cols-[17px_1fr_auto] items-center gap-2.5 px-3.5 py-[7px] text-left no-underline [[data-ledger-row]+&]:hairline-t",
    "transition-colors duration-300 ease-spring motion-reduce:transition-none",
    (href || onClick) && "focus-ring tappable cursor-pointer hover:bg-shell active:bg-sunk",
    className,
  );

  const content = (
    <>
      {tickIcon ?? (tick && <StatusTick state={tick} />)}
      {/* min-w-0 is the entire reason the row truncates instead of blowing out the grid: without
          it a grid item's default min-width:auto refuses to shrink below its content. */}
      <span className="min-w-0">
        <span className="block truncate text-body font-[650] tracking-[-.012em] text-ink">{title}</span>
        {subtitle && <span className="block truncate text-caption text-muted">{subtitle}</span>}
      </span>
      <span className="flex flex-none items-center gap-2">{meta}</span>
    </>
  );

  if (href) {
    return (
      <a data-ledger-row href={href} className={classes}>
        {content}
      </a>
    );
  }
  if (onClick) {
    return (
      <button data-ledger-row type="button" onClick={onClick} className={cx("w-full", classes)}>
        {content}
      </button>
    );
  }
  return (
    <div data-ledger-row className={classes}>
      {content}
    </div>
  );
}
