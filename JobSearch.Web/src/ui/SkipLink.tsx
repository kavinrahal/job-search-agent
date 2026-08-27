// Off screen until focused, then the first thing Tab reaches on the page.
//
// Positioned off screen rather than hidden with display:none or visibility:hidden, because both of
// those remove it from the tab order entirely, which defeats the point.
export function SkipLink({ href = "#main", children = "Skip to content" }: { href?: string; children?: string }) {
  return (
    <a
      href={href}
      className="tappable absolute top-0 -left-[9999px] z-60 rounded-br-ctl bg-ember px-4 py-2.5 text-note font-[650] text-on-ember no-underline focus:left-0"
    >
      {children}
    </a>
  );
}
