import { describe, it, expect } from "vitest";
import {
  EXPERIENCE_BUCKETS, experienceBucketPatch, nearestExperienceBucket,
  SALARY_BUCKETS, salaryBucketPatch, nearestSalaryBucket,
  applySkillDimensionAnswer,
  SPONSORSHIP_YES_PATCH,
  isSimpleDisqualifier, disqualifierLinesToObjects, disqualifiersToTextareaValue, applyDisqualifierAnswer,
} from "./criteriaWizardMapping";
import type { Disqualifier, SkillDimension } from "./jobCriteriaYaml";

describe("experienceBucketPatch", () => {
  it("returns the exact patch for every bucket, keyed by id", () => {
    for (const bucket of EXPERIENCE_BUCKETS) {
      const patch = experienceBucketPatch(bucket.id);
      expect(patch.seniorityLevel).toBe(bucket.seniorityLevel);
      expect(patch.idealMaxYears).toBe(bucket.idealMaxYears);
      expect(patch).not.toHaveProperty("id");
      expect(patch).not.toHaveProperty("label");
    }
  });

  it("shares boundaries between adjacent tiers for every non-open-ended bucket", () => {
    for (const bucket of EXPERIENCE_BUCKETS) {
      if (bucket.acceptableMaxYears === "") continue; // open-ended top bucket, no invariant to check
      expect(bucket.idealMaxYears).toBe(bucket.acceptableMinYears);
      expect(bucket.acceptableMaxYears).toBe(bucket.excludedMinYears);
    }
  });

  it("leaves the top bucket's ceiling open rather than guessing an upper bound", () => {
    const top = EXPERIENCE_BUCKETS.find(b => b.id === "6+")!;
    expect(top.acceptableMaxYears).toBe("");
    expect(top.excludedMinYears).toBe("");
  });

  it("returns an empty patch for an unknown id", () => {
    expect(experienceBucketPatch("nope")).toEqual({});
  });
});

describe("nearestExperienceBucket", () => {
  it("finds the exact bucket when the value matches one precisely", () => {
    expect(nearestExperienceBucket({ acceptableMinYears: "4" })).toBe("2-4");
  });

  it("finds the closest bucket when the value doesn't match exactly, preferring the first on a tie", () => {
    // acceptableMinYears per bucket: none=1, 1-2=2, 2-4=4, 4-6=6, 6+=10. For 5, "2-4" (dist 1)
    // and "4-6" (dist 1) tie — reduce with strict "<" keeps whichever it saw first, "2-4".
    expect(nearestExperienceBucket({ acceptableMinYears: "5" })).toBe("2-4");
  });

  it("returns null for blank or non-numeric input", () => {
    expect(nearestExperienceBucket({ acceptableMinYears: "" })).toBeNull();
    expect(nearestExperienceBucket({ acceptableMinYears: "not a number" })).toBeNull();
  });
});

describe("salaryBucketPatch", () => {
  it("returns the exact patch and attaches the given currency for every bucket", () => {
    for (const bucket of SALARY_BUCKETS) {
      const patch = salaryBucketPatch(bucket.id, "GBP");
      expect(patch.salaryMin).toBe(bucket.salaryMin);
      expect(patch.currency).toBe("GBP");
      expect(patch).not.toHaveProperty("id");
    }
  });

  it("shares the flag-below/minimum boundary for every non-open-ended bucket", () => {
    for (const bucket of SALARY_BUCKETS) {
      if (bucket.salaryMax === "") continue;
      expect(bucket.salaryFlagBelow).toBe(bucket.salaryMin);
    }
  });

  it("leaves the top bucket's ceiling open", () => {
    const top = SALARY_BUCKETS.find(b => b.id === "180+")!;
    expect(top.salaryMax).toBe("");
    expect(top.salaryFlagAbove).toBe("");
  });

  it("returns an empty patch for an unknown id", () => {
    expect(salaryBucketPatch("nope", "AUD")).toEqual({});
  });
});

describe("nearestSalaryBucket", () => {
  it("finds the exact bucket when the value matches precisely", () => {
    expect(nearestSalaryBucket({ salaryTargetMin: "110000" })).toBe("110-140");
  });

  it("returns null for blank or non-numeric input", () => {
    expect(nearestSalaryBucket({ salaryTargetMin: "" })).toBeNull();
    expect(nearestSalaryBucket({ salaryTargetMin: "abc" })).toBeNull();
  });
});

describe("applySkillDimensionAnswer", () => {
  it("is a no-op when the name is blank", () => {
    const existing: SkillDimension[] = [];
    expect(applySkillDimensionAnswer(existing, { name: "  ", strongMatch: "C#", goodMatch: "" })).toBe(existing);
  });

  it("creates index 0 on an empty list", () => {
    const result = applySkillDimensionAnswer([], { name: "Backend stack", strongMatch: "C#, .NET", goodMatch: "Java" });
    expect(result).toEqual([
      { name: "Backend stack", priority: "1", strongMatch: "C#, .NET", goodMatch: "Java", acceptable: "", excluded: "", notes: "" },
    ]);
  });

  it("replaces index 0 without touching index 1+ (dimensions added via the full editor)", () => {
    const existing: SkillDimension[] = [
      { name: "Old", priority: "1", strongMatch: "X", goodMatch: "", acceptable: "", excluded: "", notes: "" },
      { name: "Frontend stack", priority: "2", strongMatch: "React", goodMatch: "", acceptable: "", excluded: "", notes: "custom notes" },
    ];
    const result = applySkillDimensionAnswer(existing, { name: "Backend stack", strongMatch: "C#", goodMatch: "" });
    expect(result).toHaveLength(2);
    expect(result[0].name).toBe("Backend stack");
    expect(result[1]).toBe(existing[1]);
  });
});

describe("SPONSORSHIP_YES_PATCH", () => {
  it("does not include Australia-specific phrasing, since the wizard spans any country", () => {
    expect(SPONSORSHIP_YES_PATCH.sponsorshipDiscardExamples).not.toContain("Australian");
  });

  it("includes the core country-agnostic exclusion phrases", () => {
    const examples = SPONSORSHIP_YES_PATCH.sponsorshipDiscardExamples!;
    expect(examples).toContain("no visa sponsorship");
    expect(examples).toContain("unrestricted work rights required");
  });

  it("states the silence-is-not-a-negative-signal principle", () => {
    expect(SPONSORSHIP_YES_PATCH.sponsorshipNotes).toMatch(/silence is not a negative signal/i);
  });
});

describe("disqualifier helpers", () => {
  it("splits multiline text into one description-only disqualifier per non-blank line", () => {
    const result = disqualifierLinesToObjects("Gambling industry\n\nRequires cold-calling\n  ");
    expect(result).toEqual([
      { id: "", description: "Gambling industry", signals: "", notes: "" },
      { id: "", description: "Requires cold-calling", signals: "", notes: "" },
    ]);
  });

  it("isSimpleDisqualifier is true only when id/signals/notes are all blank", () => {
    const simple: Disqualifier = { id: "", description: "x", signals: "", notes: "" };
    const rich: Disqualifier = { id: "backend_not_dotnet", description: "x", signals: "Java", notes: "" };
    expect(isSimpleDisqualifier(simple)).toBe(true);
    expect(isSimpleDisqualifier(rich)).toBe(false);
  });

  it("disqualifiersToTextareaValue only surfaces simple disqualifiers, one per line", () => {
    const existing: Disqualifier[] = [
      { id: "", description: "Gambling industry", signals: "", notes: "" },
      { id: "backend_not_dotnet", description: "Not .NET", signals: "Java, Python", notes: "case by case" },
    ];
    expect(disqualifiersToTextareaValue(existing)).toBe("Gambling industry");
  });

  it("applyDisqualifierAnswer preserves rich disqualifiers untouched and replaces simple ones", () => {
    const rich: Disqualifier = { id: "backend_not_dotnet", description: "Not .NET", signals: "Java", notes: "case by case" };
    const existing: Disqualifier[] = [
      { id: "", description: "Old dealbreaker", signals: "", notes: "" },
      rich,
    ];
    const result = applyDisqualifierAnswer(existing, "New dealbreaker\nAnother one");
    expect(result).toHaveLength(3);
    expect(result[0]).toBe(rich); // rich entries preserved, order-stable at the front
    expect(result[1].description).toBe("New dealbreaker");
    expect(result[2].description).toBe("Another one");
  });

  it("round-trips a simple disqualifier through textarea -> objects -> textarea unchanged", () => {
    const existing: Disqualifier[] = [{ id: "", description: "No fully on-site roles", signals: "", notes: "" }];
    const text = disqualifiersToTextareaValue(existing);
    const roundTripped = applyDisqualifierAnswer(existing, text);
    expect(disqualifiersToTextareaValue(roundTripped)).toBe(text);
  });
});
