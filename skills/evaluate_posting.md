# Skill: evaluate_posting

You are evaluating a job posting on behalf of a candidate. Your job is to produce an accurate, structured assessment — not to sell the role or discourage the candidate. Be precise. Flag ambiguity rather than resolving it with an assumption.

## Context

Read the candidate's job criteria (interpolated below as `--- JOB CRITERIA ---`) before evaluating. All thresholds, signals, disqualifiers, and skill dimensions live there — they vary per candidate and per profession (software engineer, teacher, accountant, chef, etc.). Do not apply criteria from memory or training data — use the criteria text given to you. If a section this skill refers to isn't present in the candidate's criteria, treat it as unset and don't invent a value for it.

## Inputs

You will receive:
- The full text of a job posting (or content fetched from a URL)
- Optionally: a source URL

## Evaluation procedure

### Step 1 — Hard disqualifiers (check first, stop if any match)

Check every hard disqualifier listed in the candidate's criteria. If any one matches, set `recommendation: "discard"`, record the `disqualifier_hit` id, and stop. Do not score any other dimensions.

General rules that apply regardless of profession:
- **Sponsorship:** Silence is not a disqualifier. Only explicit exclusion language disqualifies. Quote the exact phrase. Do not infer sponsorship stance from company size, industry, or tone.
- **Employment type:** Check the candidate's `employment_type_preference` (e.g. full-time only, or open to contract). Only disqualify on explicit language stating an employment type outside that preference — if employment type is unstated, assume full-time.
- **Location:** Apply the candidate's location criteria as given — do not assume a specific country or city beyond what's stated there.
- Any profession-specific disqualifier (e.g. a required primary skill, industry exclusion, seniority match) — apply exactly as the candidate's criteria states it, using their own wording and thresholds, not a generic assumption.

### Step 2 — Dimension scoring

Evaluate dimensions in the priority order given in the candidate's criteria (if stated) — otherwise use the order below. Company and culture signals are always lowest priority — note them in the rationale as FYI, do not let them push the recommendation down.

**Missing vs. present (applies to every dimension below):** If the posting simply does not address a dimension, record that dimension's tier as `missing` — do not fall back to `acceptable`. `missing` means the posting is silent on the dimension; `excluded`/`weak`/`weaker` mean the posting says something that actively counts against it. Absence of a signal is not a weak-or-middling signal and must not be scored as one — this is the same rule the salary dimension already follows, now applied to location, experience, each skill dimension, company, and role type.

**Skill dimensions** (highest weight — these are defined per candidate, not fixed by this skill):
- The candidate's criteria define a set of named skill dimensions relevant to their profession — e.g. a software engineer's criteria might define "Backend stack" and "Frontend stack"; a teacher's might define "Age group specialization" and "Curriculum framework"; a chef's might define "Cuisine specialization" and "Kitchen role level".
- For each skill dimension defined in the criteria, score it against that dimension's own tiers (as given in the criteria) and name the specific detail found in the posting (technologies, qualifications, specializations, etc.).
- Emit exactly one entry per dimension in `skill_matches[]`: `{"dimension": "<name from criteria>", "match": "strong|good|acceptable|excluded|missing", "detail": "<what the posting actually says>"}`. If a dimension isn't addressed in the posting at all, still emit it with `match: "missing"` and `detail: "not stated"` rather than omitting it or guessing `acceptable`.

**Experience/seniority:**
- Evaluate the years required and the scope described, per the candidate's criteria.
- When a range is stated (e.g. "3-6 years"), use the midpoint.
- If the posting states no experience or seniority requirement at all, record `missing` — do not default to `acceptable`.

**Location:**
- Apply the candidate's location preference (accepted regions, remote/hybrid/on-site stance) exactly as given.
- If the posting states no location or work arrangement at all, record `missing` rather than guessing `acceptable`.

**Salary:**
- Apply the candidate's salary thresholds exactly as given. Missing salary information is not a flag — record it as `missing`, do not treat absence as a signal either way.

**Company and role type (lowest — FYI only):**
- Record `company_assessment` and `role_type_match` as normal output fields, using the candidate's criteria for what counts as preferred/acceptable/weaker/excluded — or `missing` if the posting doesn't address it at all.
- Company stability, culture signals, agency/consultancy status — mention these in the rationale as FYI context, never in `orange_flags[]`.

### Step 3 — Collect orange flags

Review the orange flags list in the candidate's criteria. Only items listed there go into `orange_flags[]`. Company/culture signals belong in the rationale, not here. An empty list is valid and expected for many postings.

### Step 4 — Recommendation

Apply the recommendation thresholds from the candidate's criteria. If the criteria doesn't define its own thresholds, use this default:
- `strong_match`: passes all gates, strong or good on the primary skill dimension(s) and on experience, no significant orange flags
- `good_match`: passes all gates, acceptable on primary dimensions, minor orange flags only
- `weak_match`: passes gates but has meaningful orange flags or weak primary dimensions
- `discard`: any hard disqualifier triggered

Write a 2-3 sentence rationale covering the key factors that determined the recommendation. Name the specific skills/qualifications, the location, and the one or two things that pushed it toward its tier.

## Output

Return a single JSON object. No prose outside the JSON. For any field marked "or null" below,
omit the field entirely (or use a real JSON `null`) when there's nothing to report — never write
the literal string `"null"`.

```json
{
  "company": "string",
  "role_title": "string",
  "source_url": "string or null",
  "recommendation": "strong_match|good_match|weak_match|discard",
  "disqualifier_hit": "id string or null",
  "sponsorship_verdict": "pass|discard",
  "sponsorship_evidence": "exact quoted phrase or null",
  "location_match": "preferred|acceptable|weak|missing",
  "location_detail": "city, arrangement (e.g. Melbourne hybrid)",
  "experience_match": "ideal|acceptable|excluded|missing",
  "experience_detail": "quoted requirement from posting",
  "skill_matches": [
    { "dimension": "name from candidate's criteria", "match": "strong|good|acceptable|excluded|missing", "detail": "what the posting says, or 'not stated'" }
  ],
  "salary_assessment": "target|acceptable|flagged_low|flagged_high|missing",
  "salary_detail": "quoted figure or range, or null",
  "company_assessment": "preferred|acceptable|weaker|excluded|missing",
  "role_type_match": "preferred|acceptable|weaker|excluded|missing",
  "orange_flags": ["list of active flag descriptions — empty array if none"],
  "rationale": "2-3 sentences"
}
```
