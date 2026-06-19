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
- **PHP:** Only disqualifies if PHP is the *primary* backend. A PHP tool mentioned alongside a dominant .NET or Java backend is not a disqualifier — note it as context only.
- **Gambling:** Company must operate in gambling/betting/wagering as its core business. An adjacent or tangential mention does not disqualify.
- **Solo engineer:** Only disqualifies if the posting explicitly states the candidate would be the *only* engineer. "Small team" or "early-stage" does not trigger this.

### Step 2 — Dimension scoring

Evaluate each dimension. Use only what the posting actually says — do not infer favourably or unfavourably beyond what is written.

**Location:**
- `preferred`: Melbourne, VIC or AU-based remote
- `acceptable`: Sydney or stated hybrid/remote for AU
- `weak`: on-site Sydney, or location unclear

**Experience:**
- Evaluate the years required *and* the scope described (see `scope_over_title` in criteria)
- `ideal`: up to 4 years required
- `acceptable`: 4-5 years required
- `excluded`: 5+ years required
- When a range is stated (e.g. "3-6 years"), use the midpoint

**Backend stack:**
- `strong`: C#, .NET, ASP.NET Core
- `good`: Java, Spring Boot
- `acceptable`: Python, Node.js, TypeScript server-side
- `excluded`: PHP as primary
- Name the specific technologies from the posting in your output

**Frontend stack:**
- `strong`: React, TypeScript
- `good`: Angular, Vue.js, Next.js
- `acceptable`: other modern JS frameworks

**Salary:**
- `target`: $120k-$140k AUD
- `acceptable`: $100k-$120k
- `flagged_low`: below $100k (surface, do not auto-discard)
- `flagged_high`: above $140k (flag as potential level mismatch)
- `missing`: not stated

**Company:**
- `preferred`: product company with clear PMF, scale-up, mid-sized (50-500)
- `acceptable`: startup with PMF, enterprise with clear product scope
- `weaker`: agency, consultancy, pre-revenue startup
- `excluded`: gambling industry

**Role type:**
- `preferred`: product engineering, full-stack with backend ownership
- `acceptable`: platform/infra with product impact, long-term embedded consultancy
- `weaker`: maintenance-focused, rotating agency
- `excluded`: cold-calling required, legacy only

### Step 3 — Collect orange flags

Review the orange flags list in `job_criteria.yaml`. List every flag that is active for this posting. Do not omit flags even if the overall recommendation is strong_match. An empty list is valid if none apply.

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
