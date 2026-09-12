import { describe, it, expect } from "vitest";
import { parseJobCriteriaYaml, serializeJobCriteriaYaml, type JobCriteriaData } from "./jobCriteriaYaml";

// Regression test for a real round-trip bug found while verifying the criteria wizard:
// serializeJobCriteriaYaml writes salary.minimum_acceptable/target_max alongside
// salary.thresholds/flag_reasons, but parseJobCriteriaYaml's isCleanMatch checks didn't allow
// that exact key combination — so saving salary data (from either the wizard or the full
// editor) and reloading silently dropped it into `extra` instead of the structured fields.
describe("salary round-trip", () => {
  it("preserves currency and salary fields through serialize -> parse", () => {
    const data: JobCriteriaData = { ...parseJobCriteriaYaml(""), currency: "GBP", salaryMin: "100000", salaryMax: "140000", salaryTargetMin: "110000", salaryFlagBelow: "100000", salaryFlagAbove: "155000" };
    const roundTripped = parseJobCriteriaYaml(serializeJobCriteriaYaml(data));
    expect(roundTripped.currency).toBe("GBP");
    expect(roundTripped.salaryMin).toBe("100000");
    expect(roundTripped.salaryMax).toBe("140000");
    expect(roundTripped.salaryTargetMin).toBe("110000");
  });

  it("does not fall back to extra for a normally-serialized salary section", () => {
    const data: JobCriteriaData = { ...parseJobCriteriaYaml(""), currency: "USD", salaryMin: "80000" };
    const roundTripped = parseJobCriteriaYaml(serializeJobCriteriaYaml(data));
    expect(roundTripped.extra).not.toHaveProperty("salary");
  });
});

// Skills replaced the old per-dimension tiered-match shape (name/priority/strong_match/
// good_match/acceptable/excluded/notes) with a flat ordered list of names — list position is
// priority. These tests cover the new shape's round-trip and, critically, that an existing
// user's already-saved old-shape YAML still loads without crashing and keeps every skill name.
describe("skills", () => {
  it("round-trips the new flat ordered-list shape through serialize -> parse", () => {
    const data: JobCriteriaData = { ...parseJobCriteriaYaml(""), skills: ["Backend stack", "Frontend stack", "Cloud platform"] };
    const roundTripped = parseJobCriteriaYaml(serializeJobCriteriaYaml(data));
    expect(roundTripped.skills).toEqual(["Backend stack", "Frontend stack", "Cloud platform"]);
    expect(roundTripped.extra).not.toHaveProperty("skills");
    expect(roundTripped.extra).not.toHaveProperty("skill_dimensions");
  });

  it("migrates an old rich tiered-match skill_dimensions array, ordered by its priority field", () => {
    const oldYaml = `
skill_dimensions:
  - name: "Frontend stack"
    priority: 2
    strong_match: [React, Angular]
    good_match: [Vue.js]
    acceptable: []
    excluded: []
    notes: "Secondary to backend"
  - name: "Backend stack"
    priority: 1
    strong_match: [C#, .NET]
    good_match: []
    acceptable: []
    excluded: [Java, Python]
    notes: "Binary — no good/acceptable tier"
`;
    const parsed = parseJobCriteriaYaml(oldYaml);
    // Names preserved, reordered to priority order (Backend=1 before Frontend=2) even though
    // Frontend appeared first in the array — tier/priority-number/notes detail dropped.
    expect(parsed.skills).toEqual(["Backend stack", "Frontend stack"]);
    expect(parsed.extra).not.toHaveProperty("skill_dimensions");
  });

  it("falls back to array order when the old shape's entries have no priority number", () => {
    const oldYaml = `
skill_dimensions:
  - name: "Backend stack"
    strong_match: [C#]
    good_match: []
    acceptable: []
    excluded: []
    notes: ""
  - name: "Frontend stack"
    strong_match: [React]
    good_match: []
    acceptable: []
    excluded: []
    notes: ""
`;
    expect(parseJobCriteriaYaml(oldYaml).skills).toEqual(["Backend stack", "Frontend stack"]);
  });

  it("migrates the older single flat {name, keywords} shape", () => {
    const oldYaml = `
skill_dimensions:
  - name: "Backend stack"
    keywords: [C#, .NET, ASP.NET Core]
`;
    expect(parseJobCriteriaYaml(oldYaml).skills).toEqual(["Backend stack"]);
  });

  it("folds the legacy cloud_platform/ai_tooling top-level keys into the skills list as bare names", () => {
    const oldYaml = `
cloud_platform:
  strong_match: [Azure]
  good_match: [AWS]
  acceptable: [GCP]
  excluded: []
  notes: "Azure preferred"
ai_tooling:
  weight: low
  notes: "Not a meaningful differentiator"
`;
    const parsed = parseJobCriteriaYaml(oldYaml);
    expect(parsed.skills).toEqual(["Cloud platform", "AI tooling"]);
    expect(parsed.extra).not.toHaveProperty("cloud_platform");
    expect(parsed.extra).not.toHaveProperty("ai_tooling");
  });

  it("does not crash and does not lose surrounding data when skill_dimensions is absent entirely", () => {
    const parsed = parseJobCriteriaYaml("target_job_titles: Software Engineer\n");
    expect(parsed.skills).toEqual([]);
    expect(parsed.targetJobTitles).toBe("Software Engineer");
  });
});
