import { useLoginUrl } from "../../hooks/useAuth";
import { Button } from "../../ui";
import { Band } from "./Band";

// Closing benefit-driven headline plus a repeat of the two hero buttons. This is the last thing a
// visitor sees before deciding, so it restates the outcome rather than introducing anything new.
//
// Bookends the hero: same `bg` tone, and it's the last section in the page so it sits under the
// page-level brass ambient glow (positioned bottom-right of the whole document, see
// LandingPage.tsx) the same way the hero sits under the ember one at the top.

export function Cta() {
  const loginUrl = useLoginUrl();

  return (
    <Band hairline="t" className="text-center">
      <h2 className="mx-auto mb-2.5 max-w-[26ch] text-[22px] leading-[1.1] font-bold tracking-[-.03em] text-balance sm:text-[28px]">
        Stop trading your evenings for job boards.
      </h2>
      <p className="mx-auto mb-6 max-w-[42ch] text-lede text-muted">
        Set your criteria once tonight. Wake up to a shortlist tomorrow.
      </p>
      <div className="flex flex-col items-center justify-center gap-2.5 sm:flex-row">
        <Button href="/register" cap>
          Create account
        </Button>
        <Button href={loginUrl} variant="ghost">
          Sign in with Google
        </Button>
      </div>
    </Band>
  );
}
