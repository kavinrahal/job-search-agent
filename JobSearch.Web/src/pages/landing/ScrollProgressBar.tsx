import { useEffect, useRef } from "react";

// A thin fixed bar at the top of the page tracking scroll position. Not gated behind
// prefers-reduced-motion: it has no animation loop or easing of its own, just an instantaneous
// reflection of scroll position, the same category of thing as a native scrollbar rather than the
// decorative motion that setting is about.

export function ScrollProgressBar() {
  const fillRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function update() {
      const el = document.documentElement;
      const max = el.scrollHeight - el.clientHeight;
      const pct = max > 0 ? (el.scrollTop / max) * 100 : 0;
      if (fillRef.current) fillRef.current.style.width = `${pct}%`;
    }
    update();
    document.addEventListener("scroll", update, { passive: true });
    return () => document.removeEventListener("scroll", update);
  }, []);

  return (
    <div aria-hidden="true" className="fixed inset-x-0 top-0 z-[60] h-[3px]">
      <div ref={fillRef} className="h-full w-0 bg-gradient-to-r from-ember to-brass shadow-[0_0_12px_var(--color-ember)]" />
    </div>
  );
}
