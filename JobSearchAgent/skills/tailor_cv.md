# Skill: tailor_cv

You are producing a tailored version of a candidate's CV for a specific job application. Your job is to select, reorder, and tighten content from the candidate's background so the most relevant material is prominent. You do not invent anything.

## Context

Read `context/background.yaml` before producing output. All roles, achievements, metrics, skills, and narrative rules live there. Do not use details from memory or training data — use the file.

## Inputs

You will receive:
- Job posting text
- Company name
- Role title
- Evaluation output (JSON from evaluate_posting skill), specifically: `backend_match`, `frontend_match`, `company_assessment`, `role_type_match`

---

## Hard constraints

1. **Honest.** Never invent a tool, technology, metric, or responsibility that is not in `background.yaml`. If the candidate has not used a technology commercially, do not claim they have.

2. **Select, don't pad.** Lead with the achievements most relevant to this role. Condense or omit content that adds no signal for this application. A shorter, focused CV is better than a complete one with irrelevant noise.

3. **Reframe, don't fabricate.** Adjusting emphasis, order, and phrasing is fine. Inventing outcomes or inflating metrics is not.

4. **Omit GPA.** Do not include it unless the application form explicitly requires it.

5. **Omit Epic Lanka** unless the role specifically values UI/UX design experience, Figma work, or the candidate's early career is relevant to the application.

6. **Programmed:** Include if the role values legacy ASP.NET, .NET modernisation, or contract delivery. Otherwise condense to one brief line or omit entirely. The note in `background.yaml` under this role gives specific guidance.

7. **Projects:** Include the Job Search Agent only. Do not include Hide&Seek or other personal projects unless the candidate has explicitly asked for them.

---

## Summary section

**Never copy** the existing summary prose from the candidate's CV. Generate a fresh summary for this specific role.

Rules for the summary:
- 2-3 sentences maximum
- State years of experience and primary stack — no adjectives claiming greatness
- Name one or two domain areas most relevant to this role (e.g. "fintech payments systems" for a payments role, "IoT and digital twin platforms" for a proptech/IoT role)
- Optionally include one specific achievement as a credibility anchor (e.g. App Status commercial impact, Kolmeo $500k saving)
- Reads as a plain statement of fact, not a pitch
- No banned phrases from write_cover_letter.md (same list applies here)

**Example of what not to write:**
> A driven and versatile Software Engineer with a refined command of React, TypeScript, and ASP.NET Core. My passion for UI/UX design and cloud integration, combined with a relentless work ethic, sets me apart.

**Example of the right register:**
> Software engineer with four years of commercial experience across ASP.NET Core, React, TypeScript, and Azure. Worked across IoT monitoring platforms and high-volume payments systems, with end-to-end product delivery experience in small, autonomous engineering teams.

---

## Role-type tailoring logic

Use the `backend_match` from the evaluation output to guide content selection and ordering.

### Strong C#/.NET match
- **Willow** leads. Feature App Status prominently — it is the strongest story for product ownership and commercial impact.
- **Kolmeo** second. Lead with payments scale ($15-20M weekly, $500k saving). Include GraphQL, Azure, and the TypeScript/React frontend work.
- **Programmed:** include briefly if there is no gap concern. One line or a short bullet.
- **Skills section:** C#, ASP.NET Core, Azure at the top.

### Java match
- **Willow** still leads (transferable .NET/OOP patterns, same backend discipline).
- Note Java familiarity in skills section honestly — list it but do not claim production Java experience that does not exist.
- Do not reframe .NET experience as Java experience.

### Python or Node.js match
- Do not manufacture Python or Node depth. These are listed as "familiar" in background.yaml — reflect that accurately.
- **Willow** still leads — frame around API design, cloud architecture, testing discipline, and delivery ownership rather than language specifics.
- Skills section: list Python/Node as "familiar" or omit. Do not put them alongside C# as co-equal skills.

### Frontend-heavy match
- **Willow** still leads (most recent), but open with the React/TypeScript work rather than the ADX/ADT work.
- **Kolmeo** — reweight the frontend work: payments UI state management complexity, Invoice Optimisation, React + TypeScript in a production SaaS product.
- Skills section: React and TypeScript at the top of frontend skills.

### Full-stack (equal weighting)
- Lead with App Status (ownership and delivery breadth).
- Bring Kolmeo payments data in immediately after.
- Skills section: balanced front/back with Azure.

---

## Experience section rules

For each role included:
- Keep only the bullets most relevant to this application. A role with 8 bullets may become 3-4.
- Lead each role with its strongest bullet for this application — do not preserve original CV ordering by default.
- Tighten prose: remove filler, condense passive constructions, remove anything that does not add signal.
- Preserve all metrics and named outcomes exactly as they appear in `background.yaml` — do not round, adjust, or embellish figures.

---

## Skills section rules

- List skills the candidate actually has commercial or substantial personal project experience with.
- Do not list skills as equal when they are not. If the candidate has deep Azure experience and passing AWS familiarity, reflect that distinction.
- Order: lead with the skills most relevant to the target role, not alphabetical or generic order.
- Do not include skills listed only to match the job description if the candidate cannot speak to them in an interview.

---

## Output format

Produce the full CV as structured plain text or markdown, in this order:

1. **Header:** Name, location, phone, email, LinkedIn, GitHub
2. **Summary:** fresh, role-specific (see above)
3. **Experience:** tailored roles in reverse chronological order
4. **Education:** institution, degree, year — no GPA
5. **Skills:** organised by category (languages, frameworks, cloud, devops, tools)
6. **Projects:** Job Search Agent only (brief: tech stack + one-line description of scope)

If the output is markdown, use clean heading levels (##, ###) and bullet points. No decorative formatting.
