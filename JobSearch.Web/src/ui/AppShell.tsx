import type { ReactNode } from "react";
import { cx } from "./cx";
import { Grain } from "./Grain";
import { SkipLink } from "./SkipLink";
import { BrandGlyph } from "./icons";

// The application frame.
//
// Desktop: sticky top bar carrying brand, nav and the account cluster.
// Mobile: the same top bar with the nav removed, and a fixed bottom tab bar instead.
//
// The bar is sticky and the grain is fixed. The grain must not live inside the scrolling region —
// see Grain's own note. The main region owns the page's only vertical scroll.

export interface AppShellProps {
  /** TopNav with NavItems. Hidden below md by TopNav itself. */
  nav?: ReactNode;
  /** The right-hand cluster: CreditPill, theme control, AccountMenu. */
  actions?: ReactNode;
  /** BottomTabs. Hidden at md and up by BottomTabs itself. */
  tabs?: ReactNode;
  children: ReactNode;
  className?: string;
}

export function AppShell({ nav, actions, tabs, children, className }: AppShellProps) {
  return (
    <div className={cx("min-h-screen bg-bg text-ink", className)}>
      <Grain />
      <SkipLink />

      <header className="hairline-b sticky top-0 z-30 bg-core">
        <div className="mx-auto flex h-[50px] max-w-7xl items-center justify-between gap-4 px-4 sm:px-6">
          <div className="flex min-w-0 items-center gap-5">
            <Brand />
            {nav}
          </div>
          <div className="flex flex-none items-center gap-2.5">{actions}</div>
        </div>
      </header>

      <main
        id="main"
        className={cx(
          "relative z-1 mx-auto max-w-7xl px-4 py-4 sm:px-6",
          // Clears the fixed tab bar, including the home indicator, so the last row of content is
          // never stranded underneath it.
          tabs ? "pb-[calc(64px+env(safe-area-inset-bottom))] md:pb-4" : undefined,
        )}
      >
        {children}
      </main>

      {tabs}
    </div>
  );
}

/** The mark and wordmark. Fixed content, so it takes no props beyond where it links. */
export function Brand({ href = "/", className }: { href?: string; className?: string }) {
  return (
    <a href={href} className={cx("flex flex-none items-center gap-2.5 no-underline focus-ring tappable", className)}>
      <span
        className="grid h-[23px] w-[23px] flex-none place-items-center rounded-avatar-sm bg-ember text-on-ember"
        style={{ boxShadow: "inset 0 1px 0 rgba(255,255,255,.3), 0 1px 3px rgba(120,40,25,.35)" }}
      >
        <BrandGlyph className="h-3 w-3" />
      </span>
      {/* translate="no" so machine translation does not render the wordmark as two English
          nouns, which is exactly what it looks like to a translator. */}
      <span translate="no" className="text-[13px] font-bold tracking-[-.02em] text-ink">
        Work<span className="text-ember">Santa</span>
      </span>
    </a>
  );
}
