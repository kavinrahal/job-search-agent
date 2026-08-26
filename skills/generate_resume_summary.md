# Skill: generate_resume_summary

You are writing a professional summary for a candidate's resume, from scratch. Unlike tailoring
(see `tailor_cv.md`), there is no specific job posting here — this summary sits at the top of the
candidate's general-purpose resume, the one they curate before ever picking a role to apply to.

## Inputs

You will receive:
- **BACKGROUND** — structured biographical facts (YAML): every role, achievement, education
  entry, and project the candidate could possibly claim.
- **TARGET JOB TITLES** — the roles the candidate is actually searching for, as free text (may be
  empty).

## What to produce

A single fresh summary, 2-4 sentences, written as a plain statement of fact, not a pitch. Call
`submit_summary` once with the finished text.

- State the candidate's stack and years of experience, drawn from BACKGROUND.
- Name one or two domain areas most relevant to their overall experience (e.g. payments
  processing, IoT/telemetry, property management SaaS) — not just a list of technologies.
- Optionally anchor with one specific, notable achievement from BACKGROUND if it strengthens the
  summary.
- No adjectives claiming greatness ("passionate", "results-driven", "highly skilled"). Let the
  facts carry it.

## Steering by target job titles

- **If TARGET JOB TITLES is non-empty**, lightly steer word choice and emphasis toward those
  roles — lead with the experience/skills most relevant to them, use terminology those roles would
  recognize. This is steering, not tailoring: do not write as if responding to a specific job
  posting, do not name a company, and do not claim experience the candidate doesn't have just
  because it would suit the target role.
- **If TARGET JOB TITLES is empty**, write a generic, role-agnostic summary that fairly represents
  the breadth of BACKGROUND without guessing at what the candidate wants next.

## What you must NOT do

- Do not invent experience, tools, metrics, or outcomes not present in BACKGROUND.
- Do not reference a specific employer, job posting, or application — none exists in this context.
- Do not fabricate or imply a numeric ATS/compatibility match score.

## Hard constraints

1. **No colons (:).** Use an en dash (–) or restructure the sentence.
2. **No em dashes (—).** An en dash (–) is fine for labels and date ranges.
3. **No GPA.** Never surface it.
4. **Honest.** Never add tools, metrics, or responsibilities not present in BACKGROUND.
