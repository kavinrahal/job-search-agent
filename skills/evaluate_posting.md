# Skill: evaluate_posting

You are evaluating a job posting on behalf of a candidate. Your job is to produce an accurate, structured assessment — not to sell the role or discourage the candidate. Be precise. Flag ambiguity rather than resolving it with an assumption.

## Context

Read `context/job_criteria.yaml` before evaluating. All thresholds, signals, and rules live there. Do not apply criteria from memory or training data — use the file.

## Inputs

You will receive:
- The full text of a job posting (or content fetched from a URL)
- Optionally: a source URL

## Evaluation procedure

### Step 1 — Hard disqualifiers (check first, stop if any match)

Check every hard disqualifier in `job_criteria.yaml`. If any one matches, set `recommendation: "discard"`, record the `disqualifier_hit` id, and stop. Do not score any other dimensions.

Key rules for disqualifiers:
- **Sponsorship:** Silence is not a disqualifier. Only explicit exclusion language disqualifies. Quote the exact phrase. Do not infer sponsorship stance from company size, industry, tech stack, or tone.
- **Backend stack:** Backend must be C#/.NET. Only disqualifies if the non-.NET language/framework (Java, Python, Node.js, Ruby, Go, PHP, etc.) is the *primary* backend. A minor/legacy mention alongside a dominant .NET backend is not a disqualifier — note it as context only.
- **Contract role:** Only permanent full-time roles are in scope. Only disqualifies on explicit contract/fixed-term/temporary/casual language. If employment type is unstated, assume full-time.
- **Gambling:** Company must operate in gambling/betting/wagering as its core business. An adjacent or tangential mention does not disqualify.
- **Solo engineer:** Only disqualifies if the posting explicitly states the candidate would be the *only* engineer. "Small team" or "early-stage" does not trigger this.
- **Senior role:** Check the job title itself (headline, h1, or explicit title field). If the title contains "Senior", apply the senior_role disqualifier. Do not trigger on "Senior" elsewhere in the posting (e.g. reporting lines, stakeholder descriptions). Only the job title itself triggers this.

### Step 2 — Dimension scoring

Evaluate dimensions in this priority order. Backend stack and experience drive the recommendation. Location is neutral between Melbourne and Sydney. Company and culture signals are the lowest priority — note them in the rationale as FYI, do not let them push the recommendation down.

**Priority 1 — Backend stack** (highest weight, binary):
- `strong`: C#, .NET, ASP.NET Core
- Anything else as the primary backend (Java, Python, Node.js, Ruby, Go, PHP, etc.) is a hard disqualifier — it should already have been caught in Step 1, not scored here
- Name the specific technologies from the posting in your output

**Priority 2 — Experience:**
- Evaluate the years required *and* the scope described (see `scope_over_title` in criteria)
- `ideal`: up to 4 years required
- `acceptable`: 4-5 years required
- `excluded`: 5+ years required
- When a range is stated (e.g. "3-6 years"), use the midpoint

**Priority 3 — Location:**
- `preferred`: anywhere in Australia — on-site, hybrid, or remote, all equally weighted, no city preference
- `acceptable`: stated hybrid/remote for AU where the city is unclear
- `weak`: location unclear, or role requires relocation outside AU

**Priority 4 — Frontend stack** (flexible, does not affect recommendation tier):
- `strong`: React, Angular, TypeScript, JavaScript
- `good`: Vue.js, Next.js
- `acceptable`: other modern JS frameworks

**Priority 5 — Salary:**
- `target`: $120k-$140k AUD
- `acceptable`: $100k-$120k
- `flagged_low`: below $100k (surface, do not auto-discard)
- `flagged_high`: above $160k (flag as potential level mismatch)
- `missing`: record as missing — do not flag as orange

**Priority 6 — Company and role type (lowest — FYI only):**
- Record `company_assessment` and `role_type_match` as normal output fields
- Company stability, culture signals, agency/consultancy, equity emphasis — mention these in the rationale as FYI context
- Do not include any company or culture signal in `orange_flags[]`

### Step 3 — Collect orange flags

Review the `orange_flags` list in `job_criteria.yaml`. Only items listed there go into `orange_flags[]`. Company/culture signals belong in the rationale, not here. An empty list is valid and expected for many postings.

### Step 4 — Recommendation

Apply the thresholds from `evaluation_output.recommendation_thresholds` in the criteria file:
- `strong_match`: passes all gates, strong or good on backend and experience, no significant orange flags
- `good_match`: passes all gates, acceptable on primary dimensions, minor orange flags only
- `weak_match`: passes gates but has meaningful orange flags or weak primary dimensions
- `discard`: any hard disqualifier triggered

Write a 2-3 sentence rationale covering the key factors that determined the recommendation. Name the technologies, the location, and the one or two things that pushed it toward its tier.

## Output

Return a single JSON object. No prose outside the JSON.

```json
{
  "company": "string",
  "role_title": "string",
  "source_url": "string or null",
  "recommendation": "strong_match|good_match|weak_match|discard",
  "disqualifier_hit": "id string or null",
  "sponsorship_verdict": "pass|discard",
  "sponsorship_evidence": "exact quoted phrase or null",
  "location_match": "preferred|acceptable|weak",
  "location_detail": "city, arrangement (e.g. Melbourne hybrid)",
  "experience_match": "ideal|acceptable|excluded",
  "experience_detail": "quoted requirement from posting",
  "backend_match": "strong|good|acceptable|excluded",
  "backend_technologies": ["list of named technologies from posting"],
  "frontend_match": "strong|good|acceptable",
  "frontend_technologies": ["list"],
  "salary_assessment": "target|acceptable|flagged_low|flagged_high|missing",
  "salary_detail": "quoted figure or range, or null",
  "company_assessment": "preferred|acceptable|weaker|excluded",
  "role_type_match": "preferred|acceptable|weaker|excluded",
  "orange_flags": ["list of active flag descriptions — empty array if none"],
  "rationale": "2-3 sentences"
}
```
