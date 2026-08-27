import type { ButtonHTMLAttributes, ReactNode } from "react";
import { Link } from "react-router-dom";
import { cx, isInternalPath, styleFor } from "./cx";
import { ArrowRightIcon } from "./icons";

// Buttons.
//
// Shape lock: a primary button is always a full pill, at either size. Ghost and subtle are always
// 9px. That pairing is what makes "the ember pill is the action" legible at a glance without
// reading the label — so size never changes the radius, only the padding and the type.
//
// The trailing *cap* is the circular well on the right of a primary button. On hover it drifts up
// and to the right, which is the one piece of personality in the system. Transform only.

export type ButtonVariant = "primary" | "ghost" | "subtle";
export type ButtonSize = "sm" | "md";

const BASE =
  "inline-flex items-center gap-2 font-[650] whitespace-nowrap no-underline select-none focus-ring tappable " +
  "transition-[background-color,color,transform,opacity] duration-400 ease-spring motion-reduce:transition-none " +
  "active:scale-[.98] disabled:pointer-events-none disabled:opacity-55 aria-disabled:pointer-events-none aria-disabled:opacity-55";

const VARIANT: Record<ButtonVariant, string> = {
  primary: "bg-ember text-on-ember rounded-pill hover:bg-ember-hi",
  ghost: "bg-transparent text-ink-2 hairline-ring-2 rounded-ctl hover:bg-shell hover:text-ink",
  subtle: "bg-shell text-ink-2 rounded-ctl hover:bg-sunk hover:text-ink",
};

/** Primary keeps a tighter right pad because the cap supplies its own optical margin. */
const SIZE: Record<ButtonSize, { primary: string; other: string }> = {
  sm: { primary: "text-note py-[5px] pr-[6px] pl-3", other: "text-note px-[11px] py-[5px]" },
  md: { primary: "text-body py-[7px] pr-2 pl-[15px]", other: "text-body px-[15px] py-[7px]" },
};

const CAP_SIZE: Record<ButtonSize, string> = {
  sm: "h-[19px] w-[19px]",
  md: "h-6 w-6",
};

const CAP_ICON: Record<ButtonSize, string> = {
  sm: "h-2.5 w-2.5",
  md: "h-3 w-3",
};

export interface ButtonProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, "className"> {
  children: ReactNode;
  variant?: ButtonVariant;
  size?: ButtonSize;
  /**
   * Show the trailing cap. `true` uses the arrow; pass an icon element for anything else (the
   * prototype uses a tick on "Save resume" and a plus on "Log application").
   */
  cap?: boolean | ReactNode;
  loading?: boolean;
  fullWidth?: boolean;
  /** Renders an <a> instead of a <button>. Button-shaped navigation is used all over the design. */
  href?: string;
  className?: string;
}

export function Button({
  children,
  variant = "primary",
  size = "md",
  cap,
  loading = false,
  fullWidth = false,
  href,
  disabled,
  className,
  type,
  ...rest
}: ButtonProps) {
  const showCap = cap !== undefined && cap !== false;
  const classes = cx(
    BASE,
    styleFor(VARIANT, variant),
    variant === "primary" ? styleFor(SIZE, size).primary : styleFor(SIZE, size).other,
    // A full-width button with a cap pushes the cap to the far edge, which is what makes it read
    // as "and then this happens" rather than as a centred label that happens to have a circle.
    fullWidth && (showCap ? "w-full justify-between" : "w-full justify-center"),
    className,
  );

  const content = (
    <>
      <span>{children}</span>
      {showCap && (
        <span
          className={cx(
            "grid flex-none place-items-center rounded-pill bg-white/20",
            styleFor(CAP_SIZE, size),
            "transition-transform duration-400 ease-spring motion-reduce:transition-none",
            !loading && "group-hover/btn:translate-x-[2px] group-hover/btn:-translate-y-px group-hover/btn:scale-106",
            loading && "motion-safe:animate-spin",
          )}
        >
          {cap === true || loading ? <ArrowRightIcon className={styleFor(CAP_ICON, size)} /> : cap}
        </span>
      )}
    </>
  );

  if (href) {
    return isInternalPath(href) ? (
      <Link to={href} className={cx("group/btn", classes)} {...(rest as object)}>
        {content}
      </Link>
    ) : (
      <a href={href} className={cx("group/btn", classes)} {...(rest as object)}>
        {content}
      </a>
    );
  }

  return (
    <button
      // Explicit, because a bare <button> inside a form submits it, and most of these do not
      // belong to one. Callers that do want a submit pass type="submit".
      type={type ?? "button"}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      className={cx("group/btn", classes)}
      {...rest}
    >
      {content}
    </button>
  );
}

export interface IconButtonProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, "className" | "children"> {
  children: ReactNode;
  /** Required by the type, not merely encouraged: an icon-only control has no accessible name without it. */
  "aria-label": string;
  variant?: Exclude<ButtonVariant, "primary">;
  size?: ButtonSize;
  className?: string;
}

const ICON_BUTTON_SIZE: Record<ButtonSize, string> = {
  sm: "h-7 w-7",
  md: "h-8 w-8",
};

export function IconButton({ children, variant = "ghost", size = "md", className, type, ...rest }: IconButtonProps) {
  return (
    <button
      type={type ?? "button"}
      className={cx(
        "inline-grid place-items-center rounded-ctl focus-ring tappable",
        "transition-[background-color,color,transform] duration-400 ease-spring motion-reduce:transition-none",
        "active:scale-[.94] disabled:pointer-events-none disabled:opacity-55",
        styleFor(ICON_BUTTON_SIZE, size),
        variant === "ghost" ? "text-muted hover:bg-shell hover:text-ink" : "bg-shell text-ink-2 hover:bg-sunk hover:text-ink",
        className,
      )}
      {...rest}
    >
      {children}
    </button>
  );
}
