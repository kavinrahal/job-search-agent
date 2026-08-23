# Skill: backfill_resume

You are migrating one candidate from the old free-text CV system to the new structured resume
system. You will receive two documents describing the same person:

- **BACKGROUND** — their structured biographical facts (YAML): every role, every achievement they
  could possibly claim, education, projects.
- **CV_BASE** — the actual resume text currently shown to them and used for every application,
  written in Markdown. This is the ground truth for what they've been presenting — it is often
  edited, reworded, shortened, or reordered relative to BACKGROUND. Some of its content may not
  exist in BACKGROUND at all.

Your job is not to judge or improve either document. It is to describe, precisely, how CV_BASE
was derived from BACKGROUND, in a form the new system can render back into something
indistinguishable from CV_BASE. Every real difference must be captured — losing content that is
currently live in CV_BASE is the one failure mode that matters here.

## What to produce

**Summary** — the literal text of CV_BASE's `## Summary` section. If it is only the placeholder
(`[Fresh summary specific to this role; see tailoring instructions]` or similar bracketed
instruction text, not a real summary), submit an empty string — the renderer already falls back to
the placeholder itself.

**Section order** — the order CV_BASE's `##` headings actually appear in, mapped to these keys:
`experience`, `education`, `skills`, `credentials`, `publications`, `volunteering`, `projects`.
Only include keys for sections CV_BASE actually has (as `included: true`); every other key from
this list should still be present in your output with `included: false`, so future intent is
explicit rather than merely absent.

**Experience overrides** — for every entry in BACKGROUND's `experience` list, in the same order,
decide:
- `included`: does this role appear anywhere in CV_BASE's `## Experience` section? A role can be
  entirely absent (compare against the whole document, not just similar-looking entries) or
  present but marked with a bracketed conditional instruction like
  `[INCLUDE ONLY IF ...; OTHERWISE OMIT]` — in the latter case, still `included: true` (it's
  currently live), and put the bracketed instruction's substance into `notes` instead, since that
  is exactly what `notes` is for.
- `companyDescriptionOverride`: only if CV_BASE's line under that role's heading differs in
  wording from BACKGROUND's `company_description` for the same role. Omit (null) if they match or
  BACKGROUND has none.
- `achievements`: for each BACKGROUND achievement in this role, find its corresponding bullet in
  CV_BASE (matching by substance, not exact text — bullets are routinely reworded). If found with
  different wording, emit `{index, included: true, textOverride: "<CV_BASE's exact wording>"}`. If
  found with identical or near-identical wording, omit that achievement's entry entirely — no
  override needed. If genuinely absent from CV_BASE, emit `{index, included: false}`.
- `extraAchievements`: any bullet in CV_BASE's list for this role that has no corresponding
  BACKGROUND achievement at all — copy its exact text. This is common, not an edge case.

**Skills section** — CV_BASE's `## Skills` section, transcribed exactly as `{label, items[]}`
pairs, one per line as it appears (e.g. `**Languages** – C#, TypeScript` becomes
`{"label": "Languages", "items": ["C#", "TypeScript"]}`). Do not attempt to reconstruct this from
BACKGROUND's skills inventory — it is intentionally hand-curated and does not follow a fixed rule.
If CV_BASE has no Skills section, submit an empty list.

**Project overrides** — same method as experience overrides, applied to BACKGROUND's `projects`
list against CV_BASE's `## Projects` section (`descriptionOverride`, `highlights` overrides,
`extraHighlights`).

## What NOT to do

Do not invent content that doesn't exist in either document. Do not "improve" wording during
transcription — `textOverride`/`extraAchievements`/etc. must be CV_BASE's exact current text,
character for character, not a paraphrase of it. Do not guess at a role/bullet correspondence you
aren't reasonably confident about — when genuinely ambiguous, prefer `extraAchievements` (treating
it as new content) over a low-confidence index match, since a wrong match silently corrupts a real
achievement's override.
