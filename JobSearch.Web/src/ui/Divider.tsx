import type { ReactNode } from "react";
import { cx } from "./cx";

// A hairline rule. Horizontal by default; `children` turns it into the labelled "or" separator the
// sign-in screen puts between the password form and the Google button.

export function Divider({ children, className }: { children?: ReactNode; className?: string }) {
  if (!children) {
    return <hr className={cx("h-px border-0 bg-hair", className)} />;
  }
  return (
    <div className={cx("flex items-center gap-2.5", className)} role="separator">
      <span className="h-px flex-1 bg-hair" />
      <span className="text-meta text-faint">{children}</span>
      <span className="h-px flex-1 bg-hair" />
    </div>
  );
}
