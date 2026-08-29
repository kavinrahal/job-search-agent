import { describe, it, expect } from "vitest";
import type { DiscoveredPosting } from "../types";
import { buildMatchRows, computeMatchScore, matchSummaryLine, scoreFromRows } from "./matchScore";

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

  it("excludes salary 'missing' from the average rather than scoring it zero", () => {
    // Only experience ideal (1) counts; salary "missing" is dropped => 100, not 50.
    expect(computeMatchScore(posting({ experienceMatch: "ideal", salaryAssessment: "missing" }))).toBe(100);
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
