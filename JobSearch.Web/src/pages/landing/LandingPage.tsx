import { useLoginUrl } from "../../hooks/useAuth";
import { Brand, Button, Kicker, ThemeToggle } from "../../ui";
import { SocialProof } from "./SocialProof";
import { Problem } from "./Problem";
import { Solution } from "./Solution";
import { HowItWorks } from "./HowItWorks";
import { Faq } from "./Faq";
import { Cta } from "./Cta";

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

export function LandingPage() {
  const loginUrl = useLoginUrl();

  return (
    <div className="relative min-h-screen overflow-hidden bg-bg text-ink">
      {/* Ambient corner glows, same treatment as the auth screen. */}
      <div className="pointer-events-none absolute -top-32 -left-32 h-96 w-96 rounded-pill bg-ember/20 blur-3xl" />
      <div className="pointer-events-none absolute -right-32 -bottom-32 h-96 w-96 rounded-pill bg-brass/20 blur-3xl" />

      <div className="relative z-1 mx-auto max-w-[1120px] px-6">
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

        <div className="relative py-11">
          <div
            aria-hidden="true"
            className="pointer-events-none absolute inset-0"
            style={{ background: "radial-gradient(66% 58% at 84% 24%, var(--color-ember-wash), transparent 62%)" }}
          />
          <div className="relative max-w-[30rem]">
            <Kicker>Handled overnight</Kicker>
            <h1 className="mt-3.5 mb-3.5 text-[27px] leading-[1.05] font-bold tracking-[-.045em] text-balance sm:text-[37px] sm:leading-[1.03]">
              Wake up to a shortlist, <span className="text-ember">not a search.</span>
            </h1>
            <p className="mb-5 max-w-[42ch] text-lede text-muted">
              Set your criteria once. Work Santa checks new postings overnight, filters out everything
              that is not a fit, and hands you a tailored CV only for the roles worth your time.
            </p>
            <div className="flex flex-col gap-2.5 sm:flex-row">
              <Button href="/register" cap className="max-sm:w-full max-sm:justify-between">
                Create account
              </Button>
              <Button href={loginUrl} variant="ghost" className="max-sm:w-full max-sm:justify-center">
                Sign in with Google
              </Button>
            </div>
            <p className="mt-4 text-meta text-faint">Invite only while in beta. No card required.</p>
          </div>
        </div>

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
