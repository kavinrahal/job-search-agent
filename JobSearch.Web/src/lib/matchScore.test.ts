import { describe, it, expect } from "vitest";
import type { DiscoveredPosting } from "../types";
import { buildMatchRows, computeMatchScore, matchSummaryLine, scoreFromRows, tierOf } from "./matchScore";

// A posting with nothing assessed — each test overrides only the dimensions it cares about.
function posting(overrides: Partial<DiscoveredPosting> = {}): DiscoveredPosting {
  return {
    id: 1,
    url: "https://example.com/job",
    source: "seek",
    title: "Senior Engineer",
    company: "Acme",
    recommendation: "strong_match",
    disqualifierHit: null,
    discoveredAt: "2026-08-29T00:00:00Z",
    evaluatedAt: "2026-08-29T00:00:00Z",
    locationMatch: null,
    locationDetail: null,
    experienceMatch: null,
    experienceDetail: null,
    skillMatches: [],
    salaryAssessment: null,
    salaryDetail: null,
    companyAssessment: null,
    roleTypeMatch: null,
    orangeFlags: [],
    rationale: null,
    ...overrides,
  };
}

describe("computeMatchScore", () => {
  it("returns null when no dimensions were assessed", () => {
    expect(computeMatchScore(posting())).toBeNull();
  });

  it("averages the per-dimension weights into a 0–100 score", () => {
    // location preferred (1) + experience ideal (1) => avg 1 => 100
    expect(computeMatchScore(posting({ locationMatch: "preferred", experienceMatch: "ideal" }))).toBe(100);
    // location acceptable (0.6) + experience acceptable (0.6) => 60
    expect(computeMatchScore(posting({ locationMatch: "acceptable", experienceMatch: "acceptable" }))).toBe(60);
  });

  it("counts 'missing' as a low weight that drags the score down, not an exclusion", () => {
    // experience ideal alone => 100.
    expect(computeMatchScore(posting({ experienceMatch: "ideal" }))).toBe(100);
    // adding a missing salary (weight 0.25) pulls it down: (1 + 0.25) / 2 = 0.625 => 63.
    expect(computeMatchScore(posting({ experienceMatch: "ideal", salaryAssessment: "missing" }))).toBe(63);
  });

  it("excludes tiers with no weight mapping", () => {
    // An unknown tier contributes no weight; the assessed real tier still scores.
    const rows = buildMatchRows(posting({ experienceMatch: "ideal", locationMatch: "not-a-real-tier" }));
    expect(rows.find(r => r.label === "Location")?.weight).toBeNull();
    expect(scoreFromRows(rows)).toBe(100);
  });

  it("counts an excluded skill as a zero weight", () => {
    expect(
      computeMatchScore(
        posting({
          skillMatches: [
            { dimension: "React", match: "strong", detail: "" },
            { dimension: "Go", match: "excluded", detail: "" },
          ],
        }),
      ),
    ).toBe(50);
  });

  it("priority-weights skill dimensions above company/role-type", () => {
    // skill strong (weight 1, priority 2) + company weaker (weight 0.3, priority 0.4).
    // Weighted: (1*2 + 0.3*0.4) / (2 + 0.4) = 2.12 / 2.4 => 88.
    // A plain equal-weight average would be (1 + 0.3) / 2 = 0.65 => 65, so weighting lifts it.
    const score = computeMatchScore(
      posting({ skillMatches: [{ dimension: "React", match: "strong", detail: "" }], companyAssessment: "weaker" }),
    );
    expect(score).toBe(88);
    expect(score).toBeGreaterThan(65);
  });

  it("weights 'missing' at 0.25 on every dimension it can appear on", () => {
    for (const p of [
      posting({ locationMatch: "missing" }),
      posting({ experienceMatch: "missing" }),
      posting({ companyAssessment: "missing" }),
      posting({ roleTypeMatch: "missing" }),
      posting({ skillMatches: [{ dimension: "Go", match: "missing", detail: "not stated" }] }),
    ]) {
      expect(buildMatchRows(p)[0].weight).toBe(0.25);
    }
  });

  it("scores a posting whose assessed dimensions are all 'missing' at 25, not null", () => {
    // Every weight is 0.25, so the weighted average collapses to 0.25 regardless of priorities => 25.
    expect(
      computeMatchScore(
        posting({
          locationMatch: "missing",
          experienceMatch: "missing",
          companyAssessment: "missing",
          roleTypeMatch: "missing",
          skillMatches: [{ dimension: "React", match: "missing", detail: "not stated" }],
        }),
      ),
    ).toBe(25);
  });
});

describe("tierOf", () => {
  it("derives the tier from the match score, ignoring the recommendation field", () => {
    // skill good (0.75, pri 2) + location preferred (1, pri 1) => (1.5 + 1) / 3 = 0.833 => 83 => strong,
    // even though recommendation says weak_match.
    expect(tierOf(posting({
      recommendation: "weak_match",
      skillMatches: [{ dimension: "React", match: "good", detail: "" }],
      locationMatch: "preferred",
    }))).toBe("strong");
    // location acceptable (0.6) + experience acceptable (0.6) => 60 => good.
    expect(tierOf(posting({
      recommendation: "strong_match",
      locationMatch: "acceptable",
      experienceMatch: "acceptable",
    }))).toBe("good");
    // location weak (0.25) + experience acceptable (0.6) => 42.5 => 43 => weak.
    expect(tierOf(posting({
      recommendation: "strong_match",
      locationMatch: "weak",
      experienceMatch: "acceptable",
    }))).toBe("weak");
  });

  it("returns null only when nothing assessable exists to score", () => {
    expect(tierOf(posting())).toBeNull();
  });
});

describe("matchSummaryLine", () => {
  it("falls back for null or empty rationale", () => {
    expect(matchSummaryLine(null)).toBe("No rationale recorded for this posting.");
    expect(matchSummaryLine("   ")).toBe("No rationale recorded for this posting.");
  });

  it("returns the first sentence when there is a boundary", () => {
    expect(matchSummaryLine("Great fit on stack. Salary is unclear though.")).toBe("Great fit on stack.");
    expect(matchSummaryLine("Strong React match! More below.")).toBe("Strong React match!");
  });

  it("falls back to the whole rationale when there is no sentence boundary", () => {
    expect(matchSummaryLine("solid all round no punctuation here")).toBe("solid all round no punctuation here");
  });

  it("caps a runaway sentence with an ellipsis", () => {
    const long = "a".repeat(300);
    const out = matchSummaryLine(long);
    expect(out.endsWith("…")).toBe(true);
    expect(out.length).toBeLessThanOrEqual(140);
  });
});
