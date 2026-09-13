import { useEffect, useRef, useState, type ReactNode } from "react";
import { cx } from "../../ui";

// Generic scroll-reveal wrapper: fades/slides its children up once, the first time they scroll
// into view. Same once-only, first-view-only philosophy as CountUp elsewhere in this system — see
// that component for the fuller rationale.
//
// Under prefers-reduced-motion the content is simply shown, with no observer and no transition at
// all, matching the reference mockup's `[data-anim="off"] .reveal` fallback.

function prefersReducedMotion(): boolean {
  return typeof matchMedia === "function" && matchMedia("(prefers-reduced-motion: reduce)").matches;
}

export interface RevealProps {
  children: ReactNode;
  /** Stagger delay in ms for a group of siblings revealing in sequence. */
  delayMs?: number;
  className?: string;
  /** The element Reveal itself renders as, e.g. "li" so it stays a valid direct child of a <ul>
   * instead of wrapping in an extra <div>. Defaults to "div". */
  as?: "div" | "li";
}

export function Reveal({ children, delayMs = 0, className, as: Tag = "div" }: RevealProps) {
  const reduced = prefersReducedMotion();
  const [inView, setInView] = useState(reduced);
  const ref = useRef<HTMLDivElement & HTMLLIElement>(null);

  useEffect(() => {
    if (reduced) return;
    const el = ref.current;
    if (!el || typeof IntersectionObserver !== "function") {
      setInView(true);
      return;
    }
    const observer = new IntersectionObserver(
      entries => {
        for (const entry of entries) {
          if (entry.isIntersecting) {
            setInView(true);
            observer.disconnect();
          }
        }
      },
      { threshold: 0.2 },
    );
    observer.observe(el);
    return () => observer.disconnect();
  }, [reduced]);

  return (
    <Tag
      ref={ref}
      className={cx(
        "transition-[opacity,transform] duration-700 ease-spring motion-reduce:transition-none",
        inView ? "translate-y-0 opacity-100" : "translate-y-6 opacity-0",
        className,
      )}
      style={delayMs ? { transitionDelay: `${delayMs}ms` } : undefined}
    >
      {children}
    </Tag>
  );
}
