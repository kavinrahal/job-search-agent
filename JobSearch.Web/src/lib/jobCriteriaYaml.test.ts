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
