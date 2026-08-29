import { describe, it, expect } from "vitest";
import { computeMatchScore, dimensionLevel, dimensionValueLabel, scoreForValue, summarizeRationale } from "./matchScore";
import type { ScorableEvaluation } from "./matchScore";

const FULL: ScorableEvaluation = {
  locationMatch: "preferred",
  experienceMatch: "ideal",
  skillMatches: [
    { match: "strong" },
    { match: "good" },
  ],
  salaryAssessment: "target",
  companyAssessment: "preferred",
  roleTypeMatch: "preferred",
};

describe("scoreForValue", () => {
  it("maps every known tier to its documented score", () => {
    expect(scoreForValue("preferred")).toBe(100);
    expect(scoreForValue("ideal")).toBe(100);
    expect(scoreForValue("strong")).toBe(100);
    expect(scoreForValue("target")).toBe(100);
    expect(scoreForValue("good")).toBe(85);
    expect(scoreForValue("acceptable")).toBe(65);
    expect(scoreForValue("weaker")).toBe(40);
    expect(scoreForValue("flagged_low")).toBe(35);
    expect(scoreForValue("flagged_high")).toBe(35);
    expect(scoreForValue("weak")).toBe(25);
    expect(scoreForValue("excluded")).toBe(0);
  });

  it("returns null for missing, empty, null, undefined, and unrecognized values", () => {
    expect(scoreForValue("missing")).toBeNull();
    expect(scoreForValue("")).toBeNull();
    expect(scoreForValue(null)).toBeNull();
    expect(scoreForValue(undefined)).toBeNull();
    expect(scoreForValue("some_future_enum_value")).toBeNull();
  });
});

describe("computeMatchScore", () => {
  it("averages every present dimension, with skill matches averaged into one component first", () => {
    // location 100, experience 100, skills avg (100+85)/2=92.5, salary 100, company 100, role 100
    // mean = (100+100+92.5+100+100+100)/6 = 98.75 -> rounds to 99
    expect(computeMatchScore(FULL)).toBe(99);
  });

  it("skips a missing salaryAssessment rather than penalizing it", () => {
    const withMissingSalary: ScorableEvaluation = { ...FULL, salaryAssessment: "missing" };
    const withoutSalaryDimension: ScorableEvaluation = { ...FULL, salaryAssessment: "" };
    expect(computeMatchScore(withMissingSalary)).toBe(computeMatchScore(withoutSalaryDimension));
  });

  it("skips empty-string dimensions (legacy rows)", () => {
    const legacy: ScorableEvaluation = {
      locationMatch: "preferred",
      experienceMatch: "",
      skillMatches: [],
      salaryAssessment: "",
      companyAssessment: "",
      roleTypeMatch: "",
    };
    expect(computeMatchScore(legacy)).toBe(100);
  });

  it("returns null when literally no dimension is present", () => {
    const empty: ScorableEvaluation = {
      locationMatch: null,
      experienceMatch: "",
      skillMatches: [],
      salaryAssessment: null,
      companyAssessment: "",
      roleTypeMatch: null,
    };
    expect(computeMatchScore(empty)).toBeNull();
  });

  it("clamps to 0-100, rounding to the nearest integer", () => {
    const worst: ScorableEvaluation = {
      locationMatch: "weak",
      experienceMatch: "excluded",
      skillMatches: [{ match: "excluded" }],
      salaryAssessment: "flagged_low",
      companyAssessment: "excluded",
      roleTypeMatch: "excluded",
    };
    // location 25, experience 0, skills 0, salary 35, company 0, role 0 -> mean = 10
    expect(computeMatchScore(worst)).toBe(10);
  });
});

describe("summarizeRationale", () => {
  it("takes the first sentence of a multi-sentence rationale", () => {
    const rationale = "Strong backend overlap with the team. Salary is slightly below target. Remote-friendly role.";
    expect(summarizeRationale(rationale)).toBe("Strong backend overlap with the team.");
  });

  it("falls back to the full rationale when there is no sentence boundary", () => {
    const rationale = "Great fit overall no punctuation here";
    expect(summarizeRationale(rationale)).toBe(rationale);
  });

  it("hard-caps an unexpectedly long sentence with an ellipsis", () => {
    const longSentence = `${"a".repeat(160)}.`;
    const result = summarizeRationale(longSentence);
    expect(result.length).toBe(140);
    expect(result.endsWith("…")).toBe(true);
  });

  it("returns a fallback message for missing rationale", () => {
    expect(summarizeRationale(null)).toBe("No rationale recorded for this posting.");
    expect(summarizeRationale(undefined)).toBe("No rationale recorded for this posting.");
    expect(summarizeRationale("   ")).toBe("No rationale recorded for this posting.");
  });
});

describe("dimensionValueLabel", () => {
  it("returns a human label for every known tier value", () => {
    for (const value of ["preferred", "ideal", "strong", "target", "good", "acceptable", "weaker", "flagged_low", "flagged_high", "weak", "excluded", "missing"]) {
      expect(dimensionValueLabel(value)).not.toBe(value);
    }
  });

  it("falls back to the raw value for an unrecognized tier", () => {
    expect(dimensionValueLabel("something_new")).toBe("something_new");
  });
});

describe("dimensionLevel", () => {
  it("buckets high/medium/low/none consistently with scoreForValue's thresholds", () => {
    expect(dimensionLevel("preferred")).toBe("high");
    expect(dimensionLevel("good")).toBe("high");
    expect(dimensionLevel("acceptable")).toBe("medium");
    expect(dimensionLevel("weak")).toBe("low");
    expect(dimensionLevel("")).toBe("none");
    expect(dimensionLevel(null)).toBe("none");
  });
});
