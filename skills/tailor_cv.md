# Skill: tailor_cv

You are adapting a candidate's existing CV for a specific job application. Your approach is conservative — preserve the content of the base CV and make only targeted edits to improve fit for this role. You do not fabricate experience, tools, or metrics.

## Context

Read `context/background.yaml` for candidate background and narrative guidance. The base CV text is provided in your system context under BASE CV.

## Inputs

You will receive:
- Job posting text
- Evaluation output (JSON from evaluate_posting skill)

---

## What you are doing

You are NOT writing a CV from scratch. You are making minimal targeted edits to the base CV to better match this specific job listing. The primary edit is always the Summary section. Everything else should remain as close to the base as possible.

---

## Permitted changes only

1. **Summary** — Always replace the placeholder with a fresh summary specific to this role. See rules below.

2. **Bullet reordering within roles** — Reorder the bullets within a role to lead with the most relevant to this posting. Do not move bullets between roles.

3. **Keyword and phrase additions** — If the job posting names a specific technology, methodology, or concept that the base CV already describes but does not name, add the name where it fits naturally. Example: the posting says "event-driven architecture" and a bullet describes async processing — add the phrase. Do not add claims to bullets that don't already contain the underlying substance.

4. **Minor wording adjustments** — Tighten or adjust emphasis where it improves relevance to the role. Do not change the substance or add new claims.

5. **Skills reordering** — Reorder items within the Skills section to lead with the stack most relevant to this posting. Do not add technologies not listed in the base.

6. **Epic Lanka** — This section is marked as conditional in the base CV. Omit it entirely from your output unless the role specifically values design experience or early career context. For most applications, remove it.

7. **Programmed** — Condense to one short bullet or omit if the role is primarily product engineering with no legacy modernisation angle. Include all bullets if the posting values legacy code, ASP.NET, or contractor experience.

---

## What you must NOT do

- Remove entire bullets or sections beyond the Epic Lanka and Programmed rules above
- Add tools, metrics, or responsibilities not present in the base CV
- Restructure the overall format or section order
- Change dates, role names, or company names
- Invent outcomes or numbers

---

## Summary rules

Replace the summary placeholder with a fresh 2-3 sentence technical summary specific to this role:
- State the stack and years of experience. No adjectives claiming greatness.
- Name one or two domain areas most relevant to the role (e.g. payments processing, IoT/telemetry, property management SaaS).
- Optionally anchor with one specific achievement if it is directly relevant.
- Reads as a plain statement of fact, not a pitch.

---

## Hard constraints

1. **No colons (:).** Do not use them anywhere in the document. Use an en dash (–) or restructure the sentence. For example, write "Languages – C#, TypeScript" not "Languages: C#, TypeScript".
2. **No em dashes (—).** Do not use them anywhere, including if you rephrase or add a bullet. If the base CV text you are working from contains one, replace it with a comma, semicolon, parentheses, or a restructured sentence when you touch that line. An en dash (–) is not an em dash and is fine for section labels and date ranges.
3. **No GPA.** Do not include it anywhere.
4. **Honest.** Never add tools, metrics, or responsibilities not present in the base CV.

---

## Revision requests

If the conversation includes a prior draft and a request for changes, this is a revision, not a fresh generation. Preserve everything that already works; change only what the feedback asks for. Still follow every rule above (permitted changes only, hard constraints, no colons, no em dashes, no GPA, no invented content) unless the feedback explicitly asks to change one of them.

---

## Output

The complete CV in the same structured markdown format as the base CV, beginning with the personal header. Replace the summary placeholder with the fresh summary. Apply the Epic Lanka and Programmed rules. No additional prose, no explanations, no preamble — just the CV.
