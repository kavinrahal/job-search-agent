import { describe, it, expect } from "vitest";
import { getMissingCriteriaFields, isCriteriaComplete } from "./criteriaCompleteness";
import { parseJobCriteriaYaml, type JobCriteriaData } from "./jobCriteriaYaml";

const COMPLETE: JobCriteriaData = {
  ...parseJobCriteriaYaml(""),
  targetJobTitles: "Software Engineer",
  candidateCurrentExperience: "2-4 years",
  skillDimensions: [{ name: "Backend stack", priority: "1", strongMatch: "C#, .NET", goodMatch: "", acceptable: "", excluded: "", notes: "" }],
  employmentTypes: ["full_time"],
  countries: "Australia",
  remoteAccepted: true,
  hybridAccepted: false,
  onsiteAccepted: false,
  salaryMin: "100000",
};

describe("getMissingCriteriaFields", () => {
  it("returns nothing for Tier1 when every non-sponsorship field is filled", () => {
    expect(getMissingCriteriaFields(COMPLETE, "Tier1")).toEqual([]);
  });

  it("returns nothing for Tier2 when target job titles is also filled", () => {
    expect(getMissingCriteriaFields(COMPLETE, "Tier2")).toEqual([]);
  });

  it("flags target job titles missing for Tier2 but not Tier1", () => {
    const data = { ...COMPLETE, targetJobTitles: "" };
    expect(getMissingCriteriaFields(data, "Tier2").map(m => m.key)).toContain("targetJobTitles");
    expect(getMissingCriteriaFields(data, "Tier1").map(m => m.key)).not.toContain("targetJobTitles");
  });

  it("flags experience missing when candidateCurrentExperience is blank", () => {
    const data = { ...COMPLETE, candidateCurrentExperience: "" };
    expect(getMissingCriteriaFields(data, "Tier1").map(m => m.key)).toContain("experience");
  });

  it("flags skill dimensions missing when there are none, or the first has no name/strongMatch", () => {
    expect(getMissingCriteriaFields({ ...COMPLETE, skillDimensions: [] }, "Tier1").map(m => m.key)).toContain("skillDimensions");
    const blankName = { ...COMPLETE, skillDimensions: [{ name: "", priority: "", strongMatch: "C#", goodMatch: "", acceptable: "", excluded: "", notes: "" }] };
    expect(getMissingCriteriaFields(blankName, "Tier1").map(m => m.key)).toContain("skillDimensions");
    const blankStrongMatch = { ...COMPLETE, skillDimensions: [{ name: "Backend", priority: "", strongMatch: "", goodMatch: "", acceptable: "", excluded: "", notes: "" }] };
    expect(getMissingCriteriaFields(blankStrongMatch, "Tier1").map(m => m.key)).toContain("skillDimensions");
  });

  it("flags employment type missing when nothing is selected", () => {
    expect(getMissingCriteriaFields({ ...COMPLETE, employmentTypes: [] }, "Tier1").map(m => m.key)).toContain("employmentTypes");
  });

  it("flags location missing when no country is set", () => {
    expect(getMissingCriteriaFields({ ...COMPLETE, countries: "" }, "Tier1").map(m => m.key)).toContain("location");
  });

  it("flags work arrangement missing when remote/hybrid/onsite are all false", () => {
    const data = { ...COMPLETE, remoteAccepted: false, hybridAccepted: false, onsiteAccepted: false };
    expect(getMissingCriteriaFields(data, "Tier1").map(m => m.key)).toContain("arrangement");
  });

  it("flags salary missing only when all three salary fields are blank", () => {
    const data = { ...COMPLETE, salaryMin: "", salaryTargetMin: "", salaryMax: "" };
    expect(getMissingCriteriaFields(data, "Tier1").map(m => m.key)).toContain("salary");
    // Any one of the three is enough to count as filled.
    expect(getMissingCriteriaFields({ ...data, salaryTargetMin: "100000" }, "Tier1").map(m => m.key)).not.toContain("salary");
  });

  it("never flags sponsorship or disqualifiers — both are legitimately optional", () => {
    const keys = getMissingCriteriaFields(parseJobCriteriaYaml(""), "Tier2").map(m => m.key);
    expect(keys).not.toContain("sponsorship");
    expect(keys).not.toContain("disqualifiers");
  });
});

describe("isCriteriaComplete", () => {
  it("mirrors getMissingCriteriaFields being empty", () => {
    expect(isCriteriaComplete(COMPLETE, "Tier1")).toBe(true);
    expect(isCriteriaComplete({ ...COMPLETE, employmentTypes: [] }, "Tier1")).toBe(false);
  });
});
