import type { MouseEvent } from "react";

// Magnetic hover: nudges a button toward the cursor within a small radius, ported from the
// reviewed mockup's `.btn.magnet` mousemove handling. Returned as plain event-handler props
// rather than a ref + effect, since Button/IconButton already forward arbitrary DOM props
// (including onMouseMove/onMouseLeave) through `{...rest}` onto the underlying <a>/<button>.

function prefersReducedMotion(): boolean {
  return typeof matchMedia === "function" && matchMedia("(prefers-reduced-motion: reduce)").matches;
}

export function magneticHoverProps<T extends HTMLElement>() {
  return {
    onMouseMove: (e: MouseEvent<T>) => {
      if (prefersReducedMotion()) return;
      const el = e.currentTarget;
      const r = el.getBoundingClientRect();
      const x = (e.clientX - r.left - r.width / 2) * 0.3;
      const y = (e.clientY - r.top - r.height / 2) * 0.3;
      el.style.transform = `translate(${x}px, ${y - 2}px)`;
    },
    onMouseLeave: (e: MouseEvent<T>) => {
      // Button's own transition (duration-400 ease-spring on transform) eases this reset back to
      // rest, so no separate CSS transition is needed here.
      e.currentTarget.style.transform = "";
    },
  };
}
