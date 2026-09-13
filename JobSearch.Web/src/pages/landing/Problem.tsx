import { Eyebrow } from "../../ui";

// Plain text section, no exotic primitive: the point is to name the overhead around a job search
// before Solution shows how the product removes it.

const PAIN_POINTS = [
  "Hours spent scanning postings that were never a real fit",
  "A CV rewritten from scratch for every single application",
  "No record of what you applied to, or what happened next",
];

export function Problem() {
  return (
    <section className="hairline-t py-11">
      <Eyebrow>The problem</Eyebrow>
      <h2 className="mt-2.5 mb-3 max-w-[32ch] text-[20px] leading-[1.15] font-bold tracking-[-.03em] text-balance sm:text-[25px]">
        Job searching eats the time you do not have.
      </h2>
      <p className="mb-5 max-w-[60ch] text-body text-muted">
        Scrolling five job boards a night. Rewriting the same CV for every application. Guessing which
        postings are even worth opening. Losing track of who you applied to and when. None of it is the
        actual job search, it is just the overhead around it.
      </p>

      <ul className="m-0 flex list-none flex-col gap-2.5 p-0">
        {PAIN_POINTS.map(point => (
          <li key={point} className="flex items-start gap-2.5">
            <span aria-hidden="true" className="mt-[7px] h-1.5 w-1.5 flex-none rounded-pill bg-ember" />
            <span className="text-body text-ink-2">{point}</span>
          </li>
        ))}
      </ul>
    </section>
  );
}
