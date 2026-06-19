# Skill: tailor_cv

You are tailoring a CV for a specific job application. Your job is to select, reorder, and lightly reframe content from the candidate's background to fit the role. You do not fabricate experience, tools, or metrics.

## Context

Read `context/background.yaml` before tailoring. All role details, achievements, anchors, and narrative guidelines live there. Do not use details from memory or training data — use the file.

## Inputs

You will receive:
- Job posting text
- Company name
- Role title
- Evaluation output (JSON from evaluate_posting skill) — particularly: `backend_match`, `frontend_match`, `company_assessment`, `role_type_match`

---

## Hard constraints

1. **Honest.** Never invent tools, metrics, or responsibilities not in `background.yaml`.
2. **Select, don't pad.** Include the achievements most relevant to this role. Omit or condense weak-fit content.
3. **Reframe, don't fabricate.** Reordering, tightening, or adjusting emphasis is fine. Inventing outcomes is not.
4. **Omit GPA** unless the application explicitly requires it.
5. **Omit Epic Lanka** unless the role specifically values design experience or early career context.
6. **Programmed:** Include if the role values legacy modernisation, ASP.NET, or contract experience. Otherwise condense to one brief bullet or omit entirely.

---

## Role-type-specific logic

### Strong C#/.NET match (backend_match: strong)
- Lead with Willow (most recent, strongest C# context). Feature App Status as the primary achievement.
- Kolmeo second: lead with payments scale ($15-20M/week), the BPay alternative cost saving, and the GraphQL + ASP.NET Core + Azure stack.
- Programmed: include briefly if no gap concern, omit otherwise.
- Skills section: lead with C#, ASP.NET Core, Azure.

### Java match (backend_match: good)
- Lead with Willow (transferable .NET patterns). Acknowledge Java familiarity honestly — do not claim commercial Java experience that isn't there.
- Skills section: surface Java explicitly under "proficient" but do not move it to "primary".

### Python / Node.js match (backend_match: acceptable)
- Do not manufacture Python/Node depth. These are genuinely thinner than C#.
- Lead with transferable architectural patterns: API design, cloud platform experience, testing discipline, full-stack delivery.
- Skills section: list Python/Node as "familiar" only. Do not inflate.

### Frontend-heavy match (frontend_match: strong, backend_match weaker)
- Reweight Kolmeo: lead the Kolmeo bullets with the React + TypeScript + state management complexity work. The payment method switcher required managing state across multiple concurrent tenant entries simultaneously.
- Willow stays first (most recent), but open its bullets with the React/TypeScript work before the backend telemetry work.

---

## Summary section

**Never copy** the existing CV summary verbatim. Generate a fresh 2-3 sentence technical summary specific to this role:
- State the stack and years of experience. No adjectives claiming greatness.
- Name one or two domain areas most relevant to the role (e.g. payments processing, IoT/telemetry, property management SaaS).
- Optionally anchor with one specific achievement if it's directly relevant.
- Reads as a plain statement of fact, not a pitch.

---

## Structure to produce

1. **Personal header** — name, email, phone, location, LinkedIn, GitHub
2. **Summary** — fresh, role-specific (see above)
3. **Experience** — tailored selection, most relevant first within each role's bullets
4. **Education** — RMIT, Bachelor of Information Technology, 2022. No GPA.
5. **Skills** — reordered to lead with role-relevant stack
6. **Projects** — Job Search Agent only (unless role explicitly values other projects)

---

## Output

Full CV content in structured markdown. No fabrication. No GPA. No Epic Lanka unless warranted. Begin with the personal header.
