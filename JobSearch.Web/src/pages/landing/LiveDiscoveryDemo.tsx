import { useEffect, useRef, useState } from "react";
import { Badge, Ledger, LedgerRow, Surface, type BadgeVariant } from "../../ui";

// The hero's animated "Live discovery" panel: loops through a scan -> reveal cycle to show the
// product working, instead of a static example. This is deliberately separate from
// SocialProof.tsx's panel below the fold, which stays static ("Example output from a real run")
// — Kavin only asked for the animation in the hero.
//
// Ported from the reviewed mockup's own hero demo script (scan bar, staggered row reveal, ticking
// scanned-count), ported to plain React state + CSS transitions rather than the mockup's direct
// DOM manipulation. No GSAP here: this has to run immediately on hero render, not wait on a lazy
// chunk, and the mockup itself proves the effect doesn't need a library.

interface Posting {
  company: string;
  role: string;
  tier: BadgeVariant;
}

// Pool of example postings the demo draws 3 from each cycle, copied verbatim from the reviewed
// mockup so the loop doesn't show the same three companies every time. Illustrative only, in the
// same tone as SocialProof's real "Delivered overnight" examples.
const POSTINGS: Posting[] = [
  { company: "Victorian Government", role: "Senior Developer", tier: "strong" },
  { company: "GHD", role: "Team Leader, Software Development", tier: "good" },
  { company: "NCS Australia", role: "Senior Software Engineer", tier: "weak" },
  { company: "Canva", role: "Backend Engineer", tier: "strong" },
  { company: "Atlassian", role: "Software Engineer, Platform", tier: "strong" },
  { company: "Telstra", role: "Full Stack Developer", tier: "good" },
  { company: "Commonwealth Bank", role: "Senior Software Engineer", tier: "good" },
  { company: "Woolworths Group", role: "Software Engineer", tier: "weak" },
  { company: "REA Group", role: "Backend Developer", tier: "strong" },
  { company: "Seek", role: "Full Stack Engineer", tier: "strong" },
  { company: "Xero", role: "Software Engineer", tier: "good" },
  { company: "Deloitte Digital", role: "Application Developer", tier: "weak" },
  { company: "Officeworks", role: "IT Support Engineer", tier: "weak" },
  { company: "Australia Post", role: "Software Engineer", tier: "good" },
  { company: "Coles Group", role: "Backend Developer", tier: "weak" },
  { company: "NAB", role: "Senior Software Engineer", tier: "strong" },
  { company: "Qantas", role: "Full Stack Developer", tier: "good" },
  { company: "CSIRO Data61", role: "Research Software Engineer", tier: "strong" },
  { company: "Culture Amp", role: "Backend Engineer", tier: "strong" },
  { company: "Envato", role: "Software Engineer", tier: "good" },
  { company: "Employment Hero", role: "Full Stack Developer", tier: "strong" },
  { company: "SafetyCulture", role: "Senior Backend Engineer", tier: "strong" },
  { company: "Zip Co", role: "Software Engineer", tier: "good" },
  { company: "Domain Group", role: "Frontend Developer", tier: "weak" },
  { company: "Service NSW", role: "Software Developer", tier: "good" },
  { company: "Bendigo Bank", role: "Software Engineer", tier: "weak" },
  { company: "Jetstar", role: "Full Stack Developer", tier: "weak" },
  { company: "Aussie Broadband", role: "Backend Developer", tier: "good" },
  { company: "Carsales", role: "Senior Software Engineer", tier: "strong" },
  { company: "MYOB", role: "Software Engineer", tier: "good" },
];

const TIER_LABEL: Record<BadgeVariant, string> = { strong: "Strong", good: "Good", weak: "Weak", live: "Live", neutral: "" };

const ROWS = 3;
const SCAN_MS = 1900;
const REVEAL_BASE_MS = 550;
const REVEAL_STEP_MS = 620;
const CYCLE_MS = SCAN_MS + 1400;
const SCAN_COUNT_INTERVAL_MS = 1100;
const START_SCAN_COUNT = 1204;

const FORMAT = new Intl.NumberFormat();

function prefersReducedMotion(): boolean {
  return typeof matchMedia === "function" && matchMedia("(prefers-reduced-motion: reduce)").matches;
}

/** Fisher-Yates shuffle, then take the first `ROWS` — guarantees 3 distinct postings per cycle. */
function pickThree(): Posting[] {
  const pool = [...POSTINGS];
  for (let i = pool.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    // eslint-disable-next-line security/detect-object-injection -- i/j are both loop-bounded array indices, not arbitrary input
    [pool[i], pool[j]] = [pool[j], pool[i]];
  }
  return pool.slice(0, ROWS);
}

export function LiveDiscoveryDemo() {
  const reduced = prefersReducedMotion();
  const [postings, setPostings] = useState<Posting[]>(() => (reduced ? POSTINGS.slice(0, ROWS) : pickThree()));
  const [revealed, setRevealed] = useState<boolean[]>(() => Array(ROWS).fill(reduced));
  const [scanCount, setScanCount] = useState(START_SCAN_COUNT);
  const [cycleKey, setCycleKey] = useState(0);
  const fillRef = useRef<HTMLSpanElement>(null);

  // The scan -> reveal cycle. Skipped entirely under reduced motion: the panel just shows a fixed,
  // fully-revealed set with no skeleton state and no re-cycling.
  useEffect(() => {
    if (reduced) return;
    const timers: number[] = [];

    function runCycle() {
      const picks = pickThree();
      setPostings(picks);
      setRevealed(Array(ROWS).fill(false));
      setCycleKey(k => k + 1);
      for (let i = 0; i < ROWS; i++) {
        timers.push(
          window.setTimeout(() => {
            setRevealed(prev => {
              const next = [...prev];
              // eslint-disable-next-line security/detect-object-injection -- i is bounded by ROWS, not arbitrary input
              next[i] = true;
              return next;
            });
          }, REVEAL_BASE_MS + i * REVEAL_STEP_MS),
        );
      }
      timers.push(window.setTimeout(runCycle, CYCLE_MS));
    }
    runCycle();

    return () => timers.forEach(clearTimeout);
  }, [reduced]);

  // Live scanning counter — cosmetic, ticks up continuously to sell "checking postings overnight".
  useEffect(() => {
    if (reduced) return;
    const id = window.setInterval(() => {
      setScanCount(c => c + Math.floor(Math.random() * 3) + 1);
    }, SCAN_COUNT_INTERVAL_MS);
    return () => clearInterval(id);
  }, [reduced]);

  // The scan bar's sweep restarts every cycle. A CSS transition only fires on a change, so
  // resetting it (right: 100%) needs a forced reflow before setting the target (right: 0%) or the
  // browser coalesces both writes and the bar never visibly sweeps.
  useEffect(() => {
    const el = fillRef.current;
    if (!el || reduced) return;
    el.style.transition = "none";
    el.style.right = "100%";
    void el.offsetWidth;
    el.style.transition = `right ${SCAN_MS}ms linear`;
    el.style.right = "0%";
  }, [cycleKey, reduced]);

  return (
    // Decorative and duplicates SocialProof's real, static panel below the fold (which is what a
    // screen reader user should hear this claim from) — hidden from the accessibility tree rather
    // than narrated mid-cycle.
    <div aria-hidden="true">
      <Surface elevation="raised" padding="none" clip>
        <div className="hairline-b flex items-center justify-between px-3.5 py-2.5">
          <span className="flex items-center gap-[7px] text-eyebrow text-muted uppercase">
            <span className="h-1.5 w-1.5 rounded-pill bg-pos motion-safe:animate-[landing-live-pulse_1.4s_ease-in-out_infinite] motion-reduce:animate-none" />
            Live discovery
          </span>
          <span className="text-meta text-faint tabular-nums">{FORMAT.format(scanCount)} scanned</span>
        </div>
        <div className="relative h-[2px] overflow-hidden bg-hair">
          <span ref={fillRef} className="absolute inset-y-0 left-0 bg-gradient-to-r from-ember to-brass" style={{ right: reduced ? "0%" : "100%" }} />
        </div>
        <Ledger>
          {postings.map((posting, i) => (
            <LedgerRow
              key={`${posting.company}-${i}`}
              tick="done"
              title={posting.company}
              subtitle={posting.role}
              meta={<Badge variant={posting.tier}>{TIER_LABEL[posting.tier]}</Badge>}
              // eslint-disable-next-line security/detect-object-injection -- i is this array's own map index, not arbitrary input
              loading={!revealed[i]}
            />
          ))}
        </Ledger>
      </Surface>
    </div>
  );
}
