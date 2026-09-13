import { Eyebrow, Timeline, TimelineItem } from "../../ui";

// First real page usage of Timeline/TimelineItem outside the in-app application history it was
// built for. Every step uses the "pending" tick: nothing here has happened yet for a first-time
// visitor, it is a sequence being previewed, not a status being reported.

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
  return (
    <section className="hairline-t py-11">
      <Eyebrow>How it works</Eyebrow>
      <h2 className="mt-2.5 mb-6 max-w-[32ch] text-[20px] leading-[1.15] font-bold tracking-[-.03em] text-balance sm:text-[25px]">
        Four steps, and three of them run without you.
      </h2>

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
    </section>
  );
}
