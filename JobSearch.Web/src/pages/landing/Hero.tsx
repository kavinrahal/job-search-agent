import { useEffect, useRef } from "react";
import { useLoginUrl } from "../../hooks/useAuth";
import { Button, Kicker } from "../../ui";
import { HeroConstellation } from "./HeroConstellation";
import { LiveDiscoveryDemo } from "./LiveDiscoveryDemo";
import { magneticHoverProps } from "./magneticHover";

function prefersReducedMotion(): boolean {
  return typeof matchMedia === "function" && matchMedia("(prefers-reduced-motion: reduce)").matches;
}

// The hero: copy on the left, the animated "Live discovery" demo panel on the right (collapsing
// to a single column below `lg`). Everything below the copy/panel split — the gradient accent
// word, the magnetic buttons, the particle constellation, and the panel's on-load entrance + 3D
// mouse tilt — is ported from the reviewed mockup's own hero, scoped to this component so
// LandingPage.tsx stays about page structure rather than hero motion detail.

export function Hero() {
  const loginUrl = useLoginUrl();
  const heroRef = useRef<HTMLDivElement>(null);
  const panelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const heroEl = heroRef.current;
    const panelEl = panelRef.current;
    if (!heroEl || !panelEl || prefersReducedMotion()) return;

    // The panel's on-load entrance is a CSS `animation` (landing-hero-panel, in landing.css), not
    // a transition — its `forwards` fill-mode holds the visible end state in the animation
    // cascade layer, which otherwise wins over the inline `transform` the tilt handler below sets.
    // Clearing `animation` alone drops back to the base rule's `opacity: 0`, so the visible state
    // has to be pinned explicitly first, or the panel vanishes right after it fades in.
    function clearEntranceAnimation() {
      panelEl!.style.opacity = "1";
      panelEl!.style.animation = "none";
      panelEl!.style.transform = "";
    }
    panelEl.addEventListener("animationend", clearEntranceAnimation, { once: true });

    function handleMouseMove(e: MouseEvent) {
      const r = heroEl!.getBoundingClientRect();
      const px = (e.clientX - r.left) / r.width - 0.5;
      const py = (e.clientY - r.top) / r.height - 0.5;
      panelEl!.style.transform = `perspective(900px) rotateY(${px * 10}deg) rotateX(${py * -10}deg)`;
    }
    function handleMouseLeave() {
      panelEl!.style.transform = "";
    }
    heroEl.addEventListener("mousemove", handleMouseMove);
    heroEl.addEventListener("mouseleave", handleMouseLeave);

    return () => {
      panelEl.removeEventListener("animationend", clearEntranceAnimation);
      heroEl.removeEventListener("mousemove", handleMouseMove);
      heroEl.removeEventListener("mouseleave", handleMouseLeave);
    };
  }, []);

  return (
    // Full-bleed decorative layer (constellation + ember wash) spans the viewport, while the copy
    // and panel stay capped at the page's reading width — the same split every Band section uses.
    // The outer wrapper is what LandingPage no longer caps, so these decorations reach both edges;
    // `overflow-hidden` clips them (and the panel's 3D tilt) to the viewport.
    <div className="relative w-full overflow-hidden">
      <HeroConstellation />
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-0"
        style={{ background: "radial-gradient(66% 58% at 84% 24%, var(--color-ember-wash), transparent 62%)" }}
      />
      <div
        ref={heroRef}
        className="relative mx-auto grid max-w-[1120px] gap-8 px-6 py-11 lg:grid-cols-[1fr_380px] lg:items-center"
      >
        {/* landing-hero-copy: each direct child floats/fades in on load, staggered by nth-child
            (landing.css) rather than a per-element inline delay — Kicker doesn't accept a `style`
            prop, and the whole column reading as one staggered unit is the point anyway. */}
        <div className="landing-hero-copy max-w-[30rem]">
          <Kicker>Handled overnight</Kicker>
          <h1 className="mt-3.5 mb-3.5 text-[27px] leading-[1.05] font-bold tracking-[-.045em] text-balance sm:text-[37px] sm:leading-[1.03]">
            Wake up to a shortlist, <span className="landing-accent">not a search.</span>
          </h1>
          <p className="mb-5 max-w-[42ch] text-lede text-muted">
            Set your criteria once. Work Santa checks new postings overnight, filters out everything
            that is not a fit, and hands you a tailored CV only for the roles worth your time.
          </p>
          <div className="flex flex-col gap-2.5 sm:flex-row">
            <Button href="/register" cap className="will-change-transform max-sm:w-full max-sm:justify-between" {...magneticHoverProps()}>
              Create account
            </Button>
            <Button
              href={loginUrl}
              variant="ghost"
              className="will-change-transform max-sm:w-full max-sm:justify-center"
              {...magneticHoverProps()}
            >
              Sign in with Google
            </Button>
          </div>
          <p className="mt-4 text-meta text-faint">Invite only while in beta. No card required.</p>
        </div>
        <div ref={panelRef} className="landing-hero-panel">
          <LiveDiscoveryDemo />
        </div>
      </div>
    </div>
  );
}
