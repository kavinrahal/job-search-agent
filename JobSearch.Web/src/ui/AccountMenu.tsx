import { useEffect, useId, useRef, useState, type KeyboardEvent as ReactKeyboardEvent, type ReactNode } from "react";
import { cx } from "./cx";
import { Avatar } from "./Avatar";

// The avatar-triggered menu holding everything that is setup rather than daily work: Resume,
// Criteria, Sources, Settings, Help, Support, Sign out.
//
// This is where the seven nav items the design cut actually went. The prototype names the account
// menu in its closing notes but never draws it, so the trigger and the item styling are ported from
// the top bar's avatar and the nav item's hover state — see the PR description.
//
// Keyboard contract: Escape closes and returns focus to the trigger, arrows move between items,
// clicking outside closes. A menu that can only be dismissed by clicking its trigger again is the
// most common way this component gets built wrong.

export interface AccountMenuItem {
  label: string;
  href?: string;
  onSelect?: () => void;
  icon?: ReactNode;
  /** Draws a hairline above this item. Used to separate Sign out from the rest. */
  separated?: boolean;
}

export interface AccountMenuProps {
  /** Shown under the avatar in the menu header, and used for the initials. */
  name: string;
  email?: string;
  items: AccountMenuItem[];
  className?: string;
}

export function AccountMenu({ name, email, items, className }: AccountMenuProps) {
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  // A Map keyed by index rather than an array: assigning into `itemRefs.current[index]` is the
  // computed-member write security/detect-object-injection flags, and a Map cannot keep a stale
  // entry for an item that has since been removed from the menu.
  const itemRefs = useRef(new Map<number, HTMLAnchorElement | HTMLButtonElement>());
  const menuId = useId();

  function close(returnFocus: boolean) {
    setOpen(false);
    if (returnFocus) triggerRef.current?.focus();
  }

  useEffect(() => {
    if (!open) return;
    function onPointerDown(e: PointerEvent) {
      if (!rootRef.current?.contains(e.target as Node)) setOpen(false);
    }
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") close(true);
    }
    document.addEventListener("pointerdown", onPointerDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("pointerdown", onPointerDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [open]);

  // Opening with the keyboard should land on the first item, not leave focus on the trigger with
  // an open menu nobody is inside.
  useEffect(() => {
    if (open) itemRefs.current.get(0)?.focus();
  }, [open]);

  function onItemKeyDown(e: ReactKeyboardEvent, index: number) {
    if (e.key !== "ArrowDown" && e.key !== "ArrowUp") return;
    e.preventDefault();
    const delta = e.key === "ArrowDown" ? 1 : -1;
    itemRefs.current.get((index + delta + items.length) % items.length)?.focus();
  }

  return (
    <div ref={rootRef} className={cx("relative", className)}>
      <button
        ref={triggerRef}
        type="button"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        aria-label={`Account menu for ${name}`}
        onClick={() => setOpen(o => !o)}
        className="rounded-avatar-sm focus-ring tappable transition-transform duration-400 ease-spring motion-reduce:transition-none active:scale-[.94]"
      >
        <Avatar name={name} />
      </button>

      {open && (
        <div
          id={menuId}
          role="menu"
          aria-label="Account"
          className="surface-shell-e2 absolute top-full right-0 z-50 mt-2 w-56 origin-top-right"
        >
          <div className="surface-core overflow-hidden py-1">
            <div className="hairline-b px-3 pt-1.5 pb-2.5">
              <p className="m-0 truncate text-control font-[650] text-ink">{name}</p>
              {email && <p className="m-0 truncate text-meta text-faint">{email}</p>}
            </div>

            {items.map((item, index) => {
              const classes = cx(
                "flex w-full items-center gap-2.5 px-3 py-[7px] text-left text-control text-ink-2 no-underline focus-ring tappable",
                "transition-[background-color,color,transform] duration-300 ease-spring motion-reduce:transition-none hover:bg-shell hover:text-ink active:scale-[.98]",
                item.separated && "mt-1 hairline-t",
              );
              const content = (
                <>
                  {item.icon && (
                    <span aria-hidden="true" className="flex-none text-faint [&>svg]:h-3.5 [&>svg]:w-3.5">
                      {item.icon}
                    </span>
                  )}
                  <span className="truncate">{item.label}</span>
                </>
              );
              const shared = {
                role: "menuitem" as const,
                tabIndex: -1,
                className: classes,
                onKeyDown: (e: ReactKeyboardEvent) => onItemKeyDown(e, index),
              };

              return item.href ? (
                <a
                  key={item.label}
                  {...shared}
                  ref={el => {
                    if (el) itemRefs.current.set(index, el);
                    else itemRefs.current.delete(index);
                  }}
                  href={item.href}
                  onClick={() => close(false)}
                >
                  {content}
                </a>
              ) : (
                <button
                  key={item.label}
                  {...shared}
                  ref={el => {
                    if (el) itemRefs.current.set(index, el);
                    else itemRefs.current.delete(index);
                  }}
                  type="button"
                  onClick={() => {
                    close(false);
                    item.onSelect?.();
                  }}
                >
                  {content}
                </button>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}
