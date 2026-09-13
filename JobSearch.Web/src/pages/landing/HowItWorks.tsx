import { lazy, Suspense, useRef } from "react";
import { Eyebrow, Timeline, TimelineItem } from "../../ui";

// First real page usage of Timeline/TimelineItem outside the in-app application history it was
// built for. Every step uses the "pending" tick: nothing here has happened yet for a first-time
// visitor, it is a sequence being previewed, not a status being reported.

// Prototype scroll animation, lazy-loaded so GSAP + ScrollTrigger (see TimelineDraw.tsx) load as
// their own chunk only for a landing page visit, never as part of the main app bundle the
// authenticated dashboard ships. See TimelineDraw.tsx for why this uses a transform rather than
// DrawSVG, and why it draws in one step at a time.
const TimelineDraw = lazy(() => import("./TimelineDraw").then(m => ({ default: m.TimelineDraw })));

function prefersReducedMotion(): boolean {
  return typeof matchMedia === "function" && matchMedia("(prefers-reduced-motion: reduce)").matches;
}

const STEPS = [
  {
    title: "Set your criteria",
    detail: "Location, stack, salary range, must haves and dealbreakers. A few minutes, once.",
  },
  {
    title: "It watches while you do not",
    detail: "New postings are checked against your criteria every night and scored, not just keyword matched.",
  },
  {
    title: "Get a tailored CV for the ones worth it",
    detail: "No CV writing from scratch. An evaluated posting and a targeted CV are waiting when you are ready to apply.",
  },
  {
    title: "Keep every application in one place",
    detail: "Track status end to end, and get follow up questions answered in your own voice instead of starting from a blank box.",
  },
];

export function HowItWorks() {
  const timelineRef = useRef<HTMLDivElement>(null);
  // Computed once per render, same as CountUp's own reduced-motion check elsewhere on this page —
  // a reader with the setting on never triggers the dynamic import at all, and the timeline
  // simply renders its normal, fully-drawn end state with no animation layered on top.
  const reduced = prefersReducedMotion();

  return (
    <section className="hairline-t py-11">
      <Eyebrow>How it works</Eyebrow>
      <h2 className="mt-2.5 mb-6 max-w-[32ch] text-[20px] leading-[1.15] font-bold tracking-[-.03em] text-balance sm:text-[25px]">
        Four steps, and three of them run without you.
      </h2>

      <div ref={timelineRef}>
        <Timeline className="max-w-[46ch]">
          {STEPS.map((step, index) => (
            <TimelineItem
              key={step.title}
              state="pending"
              title={step.title}
              detail={step.detail}
              last={index === STEPS.length - 1}
            />
          ))}
        </Timeline>
      </div>

      {!reduced && (
        <Suspense fallback={null}>
          <TimelineDraw containerRef={timelineRef} />
        </Suspense>
      )}
    </section>
  );
}
