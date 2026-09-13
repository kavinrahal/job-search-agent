import { Accordion, Eyebrow } from "../../ui";

const ITEMS = [
  {
    question: "Is this free right now?",
    answer: "Yes. Work Santa is invite only while in beta and free to use, no card required.",
  },
  {
    question: "Do I have to connect Gmail?",
    answer:
      "No. Gmail is optional. Turning it off just means you track applications yourself instead of automatically; posting discovery and CV tailoring work exactly the same either way.",
  },
  {
    question: "Will it submit applications for me?",
    answer:
      "No. Work Santa prepares a tailored CV, cover letter, and answers to any application questions. You still hit submit yourself, then it picks up tracking the response automatically from there.",
  },
  {
    question: "What if I need visa sponsorship?",
    answer:
      "Tell it your situation once. It filters out postings that will not sponsor and, separately, postings restricted to citizens or permanent residents, so you are not wasting time on roles that were never open to you.",
  },
  {
    question: "Which postings does it actually check?",
    answer: "Company career pages plus general job board listings, checked automatically every night against your criteria.",
  },
];

export function Faq() {
  return (
    <section className="hairline-t py-11">
      <Eyebrow>Questions</Eyebrow>
      <h2 className="mt-2.5 mb-5 max-w-[32ch] text-[20px] leading-[1.15] font-bold tracking-[-.03em] text-balance sm:text-[25px]">
        Before you sign up.
      </h2>

      <Accordion items={ITEMS} className="max-w-[62ch]" />
    </section>
  );
}
