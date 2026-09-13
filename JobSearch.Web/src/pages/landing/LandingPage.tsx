import { useEffect, useRef } from "react";
import { Brand, Button, ThemeToggle } from "../../ui";
import "./landing.css";
import { Hero } from "./Hero";
import { SocialProof } from "./SocialProof";
import { Problem } from "./Problem";
import { Solution } from "./Solution";
import { HowItWorks } from "./HowItWorks";
import { Faq } from "./Faq";
import { Cta } from "./Cta";
import { ScrollProgressBar } from "./ScrollProgressBar";

function prefersReducedMotion(): boolean {
  return typeof matchMedia === "function" && matchMedia("(prefers-reduced-motion: reduce)").matches;
}

// The logged-out marketing page (prototype section 1), rewritten from a single hero-only screen
// into a full narrative: hero, social proof, problem, solution, how it works, FAQ, closing CTA.
// This is the first page in src/pages to split into its own folder. See the other files in
// src/pages/landing/ for the remaining sections, kept separate to stay under the repo's per-file
// line cap and because each section is independently reviewable copy.
//
// The hero headline sizes remain the one place this page steps outside the token type scale: the
// Slate scale is built for dense in-app UI and tops out at ~25px (text-stat), while a marketing
// hero needs a genuine display size. Every colour, spacing, radius, shadow and the body copy still
// resolve through tokens. `Kicker` appears exactly once on the whole page, here in the hero, and
// every other section label below uses the quieter `Eyebrow` instead, per Kicker's own
// once-per-screen convention.
//
// Every section below is a full-bleed band (background spans the viewport) with its reading
// content capped at the same width as the header/hero here — see Band.tsx. The two ambient corner
// glows stay on this outermost, full-page container rather than per-band: it grows to the height
// of the whole document, so the top-left one lands behind the hero and the bottom-right one lands
// behind Cta, the last section, giving CTA its own bookend glow for free.

export function LandingPage() {
  const glow1Ref = useRef<HTMLDivElement>(null);
  const glow2Ref = useRef<HTMLDivElement>(null);

  // Scroll-linked parallax on top of the two glows' own continuous CSS drift (landing.css). Capped
  // so it never noticeably outruns the drift keyframes at the top of the page.
  useEffect(() => {
    if (prefersReducedMotion()) return;
    function update() {
      const y = window.scrollY;
      if (glow1Ref.current) glow1Ref.current.style.marginTop = `${Math.min(y * 0.25, 140)}px`;
      if (glow2Ref.current) glow2Ref.current.style.marginBottom = `${Math.min(y * 0.15, 100)}px`;
    }
    update();
    document.addEventListener("scroll", update, { passive: true });
    return () => document.removeEventListener("scroll", update);
  }, []);

  return (
    <div className="relative min-h-screen overflow-hidden bg-bg text-ink">
      <ScrollProgressBar />

      {/* Ambient corner glows, same treatment as the auth screen, plus continuous drift + scroll
          parallax layered on top (see landing.css and the effect above). */}
      <div ref={glow1Ref} className="landing-glow-1 pointer-events-none absolute -top-32 -left-32 h-96 w-96 rounded-pill bg-ember/20 blur-3xl" />
      <div ref={glow2Ref} className="landing-glow-2 pointer-events-none absolute -right-32 -bottom-32 h-96 w-96 rounded-pill bg-brass/20 blur-3xl" />

      <section className="relative z-1 w-full">
        <div className="mx-auto max-w-[1120px] px-6">
          <header className="hairline-b flex items-center justify-between gap-4 py-4">
            <Brand />
            {/* The two auth buttons collapse below sm. The hero already carries the CTAs there, so
                the bar stays just the mark and the theme control, matching the prototype's mobile
                landing which shows no nav buttons at all. */}
            <div className="flex items-center gap-2.5">
              <ThemeToggle />
              <Button href="/signin" variant="ghost" size="sm" className="max-sm:hidden">
                Sign in
              </Button>
              <Button href="/register" cap size="sm" className="max-sm:hidden">
                Create account
              </Button>
            </div>
          </header>

          <Hero />
        </div>
      </section>

      <div className="relative z-1">
        <SocialProof />
        <Problem />
        <Solution />
        <HowItWorks />
        <Faq />
        <Cta />
      </div>
    </div>
  );
}
