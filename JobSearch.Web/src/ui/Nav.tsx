import type { ReactNode } from "react";
import { cx } from "./cx";

// Desktop nav and the mobile tab bar. Both are presentational: they take an `active` flag and an
// `href`, so whatever router the app uses stays the caller's business.
//
// The design caps this at four destinations. That is not a layout constraint, it is the reason the
// mobile tab bar can exist at all — eleven nav items have no mobile answer that is not a hamburger,
// and everything that got cut moved into the account menu, where it belongs, because it is setup
// rather than daily work.

export function TopNav({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <nav aria-label="Main" className={cx("hidden gap-px md:flex", className)}>
      {children}
    </nav>
  );
}

export interface NavItemProps {
  href: string;
  active?: boolean;
  children: ReactNode;
  className?: string;
}

export function NavItem({ href, active = false, children, className }: NavItemProps) {
  return (
    <a
      href={href}
      aria-current={active ? "page" : undefined}
      className={cx(
        "rounded-ctl px-2.5 py-[5px] text-control no-underline focus-ring tappable",
        "transition-[background-color,color] duration-300 ease-spring motion-reduce:transition-none",
        active ? "hairline-ring bg-shell font-bold text-ink" : "font-[550] text-muted hover:bg-shell hover:text-ink",
        className,
      )}
    >
      {children}
    </a>
  );
}

export function BottomTabs({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <nav
      aria-label="Main"
      className={cx(
        "hairline-t fixed inset-x-0 bottom-0 z-30 flex justify-around bg-core pt-[7px] md:hidden",
        // The home indicator on a modern phone sits inside the viewport. Without this the last
        // 34px of the tab bar is under it and the labels are unreadable.
        "pb-[max(9px,env(safe-area-inset-bottom))]",
        className,
      )}
    >
      {children}
    </nav>
  );
}

export interface TabProps {
  href: string;
  active?: boolean;
  icon: ReactNode;
  label: string;
  className?: string;
}

export function Tab({ href, active = false, icon, label, className }: TabProps) {
  return (
    <a
      href={href}
      aria-current={active ? "page" : undefined}
      className={cx(
        "flex flex-1 flex-col items-center gap-[3px] rounded-ctl py-1 text-[8.5px] font-[600] no-underline focus-ring tappable",
        "transition-[color,transform] duration-300 ease-spring motion-reduce:transition-none active:scale-[.94]",
        active ? "text-ember" : "text-faint hover:text-ink",
        className,
      )}
    >
      <span aria-hidden="true" className="[&>svg]:h-[15px] [&>svg]:w-[15px]">
        {icon}
      </span>
      {label}
    </a>
  );
}
