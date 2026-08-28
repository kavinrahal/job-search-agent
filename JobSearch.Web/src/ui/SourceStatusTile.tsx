import { cx } from "./cx";
import { CheckIcon } from "./icons";

// The rectangular status tile in Sources' Automatic/Alert-based grids — supersedes the flex-wrapped
// Chip pills that used to render both lists. A Chip says "choose some of these"; these lists are
// that, but the prototype also wants each one's current state legible without a click (On/Off, or
// "Needs setup" for the alert-based ones, which cannot do anything until Gmail is connected
// regardless of whether they are selected). A tile carries both in one glance; a chip only carries
// selection.

export interface SourceStatusTileProps {
  label: string;
  active: boolean;
  /** Caption shown while not active. "Off" for automatic sources, "Needs setup" for alert-based. */
  offLabel?: string;
  onClick: () => void;
  disabled?: boolean;
  className?: string;
}

export function SourceStatusTile({ label, active, offLabel = "Off", onClick, disabled, className }: SourceStatusTileProps) {
  return (
    <button
      type="button"
      role="checkbox"
      aria-checked={active}
      disabled={disabled}
      onClick={onClick}
      className={cx(
        "rounded-ctl p-[10px] text-left transition-colors duration-300 focus-ring tappable",
        "disabled:pointer-events-none disabled:opacity-55",
        active ? "bg-ember-wash shadow-[inset_0_0_0_1px_var(--color-ember)]" : "surface-sunk hover:bg-shell",
        className,
      )}
    >
      <span className="flex items-center justify-between gap-1.5">
        <span className={cx("truncate text-control font-[650]", active ? "text-ink" : "text-muted")}>{label}</span>
        {active && <CheckIcon strokeWidth={2.4} className="h-[15px] w-[15px] flex-none text-ember" />}
      </span>
      <span className="mt-[3px] block text-meta text-faint">{active ? "On" : offLabel}</span>
    </button>
  );
}
