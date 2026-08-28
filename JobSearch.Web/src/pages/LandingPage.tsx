import { useLoginUrl } from "../hooks/useAuth";
import { Badge, Brand, Button, Kicker, Ledger, LedgerRow, Surface, ThemeToggle } from "../ui";

// The logged-out marketing page (prototype section 1). A top bar, a hero, and the
// "delivered overnight" proof panel beside it on desktop, dropping below the fold on mobile.
//
// The two hero headline sizes are the one place this page steps outside the token type scale: the
// Slate scale is built for dense in-app UI and tops out at ~25px (text-stat), while a marketing
// hero needs a genuine display size. The prototype hardcodes these too (its .doc/hero h1 use
// clamp/px, not the app scale), so they live here as arbitrary values. Every colour, spacing,
// radius, shadow and the body copy still resolve through tokens.

// Three sample matches, mirroring the prototype's proof panel. Plain rows, not links — this is
// proof the product works, not a set of actions to take.
const PROOF = [
  { company: "Victorian Government", role: "Senior Developer", badge: "Strong" as const, variant: "strong" as const },
  { company: "GHD", role: "Team Leader, Software Development", badge: "Good" as const, variant: "good" as const },
  { company: "NCS Australia", role: "Senior Software Engineer", badge: "Weak" as const, variant: "weak" as const },
];

function ProofPanel() {
  return (
    <Surface elevation="raised" padding="none" clip>
      <div className="hairline-b flex items-center justify-between px-3.5 py-2.5">
        <span className="text-eyebrow text-muted uppercase">Delivered overnight</span>
        <span className="text-meta text-faint">6:12am</span>
      </div>
      <Ledger>
        {PROOF.map(item => (
          <LedgerRow
            key={item.company}
            tick="done"
            title={item.company}
            subtitle={item.role}
            meta={<Badge variant={item.variant}>{item.badge}</Badge>}
          />
        ))}
      </Ledger>
    </Surface>
  );
}

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
          {/* The two auth buttons collapse below sm — the hero already carries the CTAs there, so
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

        <div className="relative grid items-center gap-8 py-11 lg:grid-cols-[1fr_380px] lg:gap-12">
          <div
            aria-hidden="true"
            className="pointer-events-none absolute inset-0"
            style={{ background: "radial-gradient(66% 58% at 84% 24%, var(--color-ember-wash), transparent 62%)" }}
          />
          <div className="relative max-w-[30rem]">
            <Kicker>Handled overnight</Kicker>
            <h1 className="mt-3.5 mb-3.5 text-[27px] leading-[1.05] font-bold tracking-[-.045em] text-balance sm:text-[37px] sm:leading-[1.03]">
              Stop rewriting the same CV <span className="text-ember">forty times.</span>
            </h1>
            <p className="mb-5 max-w-[40ch] text-lede text-muted">
              Set your criteria once. Work Santa checks new postings while you sleep, then writes the
              CV for the ones worth your time.
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

          <div className="relative">
            <ProofPanel />
          </div>
        </div>
      </div>
    </div>
  );
}
