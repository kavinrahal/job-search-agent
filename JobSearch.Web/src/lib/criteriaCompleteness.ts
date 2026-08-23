import type { JobCriteriaData } from "./jobCriteriaYaml";

// Single source of truth for "is criteria actually filled in enough to be useful" — used by
// the onboarding wizard's per-step Next validation, the full editor's inline warnings and
// save-blocking, and the dashboard nudge banner, so the three surfaces can never disagree
// about what's missing.
//
// Sponsorship is deliberately NOT part of this list. It's the one legitimately optional/
// conditional section: a candidate who doesn't need sponsorship correctly leaves every
// sponsorship field blank ("Leave blank if this doesn't apply to you" — JobCriteriaEditor's
// own copy), so "blank" there is a valid complete answer, not a missing one. The wizard still
// requires *choosing* Yes/No (a UX concern — don't let someone fall through without ever
// considering it), but that choice has no equivalent field to check for in the full editor,
// which has no yes/no toggle at all, only free text.
//
// Disqualifiers are excluded for the same reason the wizard is the only place that still
// offers a Skip: a candidate with no dealbreakers worth naming is a completely normal,
// complete answer.
export interface MissingCriteriaField {
  key: string;
  label: string;
}

export function getMissingCriteriaFields(data: JobCriteriaData, tier: string): MissingCriteriaField[] {
  const missing: MissingCriteriaField[] = [];

  // Tier 2 only — it drives Tier2's automatic discovery and doesn't apply to Tier 1 at all
  // (see JobCriteriaEditor.tsx's own comment on why the field itself is hidden for Tier 1).
  if (tier === "Tier2" && !data.targetJobTitles.trim())
    missing.push({ key: "targetJobTitles", label: "Target job titles" });

  if (!data.candidateCurrentExperience.trim())
    missing.push({ key: "experience", label: "Experience" });

  const primarySkill = data.skillDimensions[0];
  if (!primarySkill || !primarySkill.name.trim() || !primarySkill.strongMatch.trim())
    missing.push({ key: "skillDimensions", label: "Skill dimensions" });

  if (data.employmentTypes.length === 0)
    missing.push({ key: "employmentTypes", label: "Employment type" });

  if (!data.countries.trim())
    missing.push({ key: "location", label: "Location" });

  if (!data.remoteAccepted && !data.hybridAccepted && !data.onsiteAccepted)
    missing.push({ key: "arrangement", label: "Work arrangement (remote / hybrid / on-site)" });

  if (!data.salaryMin.trim() && !data.salaryTargetMin.trim() && !data.salaryMax.trim())
    missing.push({ key: "salary", label: "Salary" });

  return missing;
}

export function isCriteriaComplete(data: JobCriteriaData, tier: string): boolean {
  return getMissingCriteriaFields(data, tier).length === 0;
}
