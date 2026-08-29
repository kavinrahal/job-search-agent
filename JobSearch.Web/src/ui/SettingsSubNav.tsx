import { Link } from "react-router-dom";
import { cx, isInternalPath } from "./cx";

// The left-hand section list on Settings: Account / Resume / Criteria / Sources / Billing / Help.
//
// Two kinds of item share one visual language. Criteria, Sources and Help already have their own
// full pages, so those items are real navigation (Link for an in-app route, <a> otherwise) — this
// component never re-renders their content. Account, Resume and Billing don't have separate pages
// today, so those items are local tabs: the caller tracks `activeKey` and swaps what renders
// beside this nav. An item is a tab exactly when it has no `href`.

export interface SettingsSubNavItem {
  key: string;
  label: string;
  /** Present -> real navigation, rendered as a link. Absent -> a local tab, rendered as a button. */
  href?: string;
}

export interface SettingsSubNavProps {
  items: SettingsSubNavItem[];
  activeKey: string;
  onSelect: (key: string) => void;
  className?: string;
}

function itemClasses(active: boolean): string {
  return cx(
    "block w-full rounded-ctl px-2.5 py-[7px] text-left text-control whitespace-nowrap no-underline focus-ring tappable",
    "transition-[background-color,color] duration-300 ease-spring motion-reduce:transition-none",
    active ? "hairline-ring bg-shell font-bold text-ink" : "font-[550] text-muted hover:bg-shell hover:text-ink",
  );
}

export function SettingsSubNav({ items, activeKey, onSelect, className }: SettingsSubNavProps) {
  return (
    <nav aria-label="Settings sections" className={cx("surface-shell-e1", className)}>
      <div className="surface-core flex flex-col gap-px p-[6px]">
        {items.map(item => {
          if (item.href) {
            // Active when this item's own page (SettingsShell) is the one rendering the nav —
            // SettingsPage itself never passes a matching activeKey for an href item, since a
            // click there always navigates away.
            return isInternalPath(item.href) ? (
              <Link key={item.key} to={item.href} className={itemClasses(item.key === activeKey)}>
                {item.label}
              </Link>
            ) : (
              <a key={item.key} href={item.href} className={itemClasses(item.key === activeKey)}>
                {item.label}
              </a>
            );
          }
          return (
            <button key={item.key} type="button" onClick={() => onSelect(item.key)} className={itemClasses(item.key === activeKey)}>
              {item.label}
            </button>
          );
        })}
      </div>
    </nav>
  );
}
