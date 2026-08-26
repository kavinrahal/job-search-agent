import { describe, it, expect } from "vitest";
import {
  getExperienceOverride, setExperienceOverride,
  getProjectOverride, setProjectOverride,
  orderedBulletRows, upsertItemOverride, moveBulletOverride,
} from "./resumeOverrides";
import type { ItemOverride, ExperienceOverride, ProjectOverride } from "../types";

describe("getExperienceOverride / setExperienceOverride", () => {
  it("returns a default (included, no overrides) row when none exists yet", () => {
    const result = getExperienceOverride([], 0);
    expect(result).toEqual({
      experienceIndex: 0, included: true, companyDescriptionOverride: null, achievements: [], extraAchievements: [], notes: null,
    });
  });

  it("returns the existing row when one is already present", () => {
    const overrides: ExperienceOverride[] = [{ experienceIndex: 1, included: false, companyDescriptionOverride: "x", achievements: [], extraAchievements: [], notes: null }];
    expect(getExperienceOverride(overrides, 1).included).toBe(false);
  });

  it("upserts without disturbing other entries", () => {
    const overrides: ExperienceOverride[] = [{ experienceIndex: 0, included: true, companyDescriptionOverride: null, achievements: [], extraAchievements: [], notes: null }];
    const result = setExperienceOverride(overrides, 1, { included: false });
    expect(result).toHaveLength(2);
    expect(result.find(o => o.experienceIndex === 0)!.included).toBe(true);
    expect(result.find(o => o.experienceIndex === 1)!.included).toBe(false);
  });

  it("patches an existing entry in place rather than duplicating it", () => {
    const overrides: ExperienceOverride[] = [{ experienceIndex: 0, included: true, companyDescriptionOverride: null, achievements: [], extraAchievements: [], notes: null }];
    const result = setExperienceOverride(overrides, 0, { companyDescriptionOverride: "New description." });
    expect(result).toHaveLength(1);
    expect(result[0].companyDescriptionOverride).toBe("New description.");
    expect(result[0].included).toBe(true);
  });
});

describe("getProjectOverride / setProjectOverride", () => {
  it("returns a default row when none exists yet", () => {
    expect(getProjectOverride([], 2)).toEqual({
      projectIndex: 2, included: true, descriptionOverride: null, highlights: [], extraHighlights: [],
    });
  });

  it("upserts without disturbing other entries", () => {
    const overrides: ProjectOverride[] = [{ projectIndex: 0, included: true, descriptionOverride: null, highlights: [], extraHighlights: [] }];
    const result = setProjectOverride(overrides, 0, { included: false });
    expect(result).toHaveLength(1);
    expect(result[0].included).toBe(false);
  });
});

describe("orderedBulletRows", () => {
  const BASE = ["A", "B", "C"];

  it("returns base items in natural order when there are no overrides", () => {
    expect(orderedBulletRows(BASE, []).map(r => r.text)).toEqual(["A", "B", "C"]);
  });

  it("applies a text override without changing position", () => {
    const overrides: ItemOverride[] = [{ index: 1, included: true, textOverride: "B, rewritten", order: null }];
    expect(orderedBulletRows(BASE, overrides).map(r => r.text)).toEqual(["A", "B, rewritten", "C"]);
  });

  it("marks an excluded item but keeps it in the row list (caller decides how to show it)", () => {
    const overrides: ItemOverride[] = [{ index: 1, included: false, textOverride: null, order: null }];
    const rows = orderedBulletRows(BASE, overrides);
    expect(rows.map(r => r.included)).toEqual([true, false, true]);
  });

  it("an explicit order always wins over natural index, even when they'd numerically collide", () => {
    // C (index 2) explicitly moved to order 0 — must lead, even though A/B are naturally at 0/1.
    const overrides: ItemOverride[] = [{ index: 2, included: true, textOverride: null, order: 0 }];
    expect(orderedBulletRows(BASE, overrides).map(r => r.index)).toEqual([2, 0, 1]);
  });

  it("sorts multiple explicitly-ordered items by their order value", () => {
    const overrides: ItemOverride[] = [
      { index: 0, included: true, textOverride: null, order: 2 },
      { index: 2, included: true, textOverride: null, order: 0 },
    ];
    // index 2 -> order 0, index 1 -> natural (group 1), index 0 -> order 2
    expect(orderedBulletRows(BASE, overrides).map(r => r.index)).toEqual([2, 0, 1]);
  });
});

describe("moveBulletOverride", () => {
  const BASE = ["A", "B", "C"];

  it("moves a natural-order item up and writes an explicit order for every item", () => {
    const result = moveBulletOverride(BASE, [], 1, -1); // move B up, ahead of A
    const rows = orderedBulletRows(BASE, result);
    expect(rows.map(r => r.index)).toEqual([1, 0, 2]);
    // Every non-extra bullet gets an explicit order now, not just the two that moved.
    expect(result).toHaveLength(3);
    expect(result.every(o => o.order !== null)).toBe(true);
  });

  it("moves an item down", () => {
    const result = moveBulletOverride(BASE, [], 0, 1); // move A down, behind B
    expect(orderedBulletRows(BASE, result).map(r => r.index)).toEqual([1, 0, 2]);
  });

  it("is a no-op moving the first item up", () => {
    const result = moveBulletOverride(BASE, [], 0, -1);
    expect(result).toEqual([]);
  });

  it("is a no-op moving the last item down", () => {
    const result = moveBulletOverride(BASE, [], 2, 1);
    expect(result).toEqual([]);
  });

  it("preserves each bullet's text override and included state across the reorder", () => {
    const overrides: ItemOverride[] = [
      { index: 0, included: false, textOverride: "A, reworded", order: null },
      { index: 2, included: true, textOverride: "C, reworded", order: null },
    ];
    const result = moveBulletOverride(BASE, overrides, 1, -1); // move B ahead of A
    const rows = orderedBulletRows(BASE, result);
    expect(rows.map(r => ({ index: r.index, text: r.text, included: r.included }))).toEqual([
      { index: 1, text: "B", included: true },
      { index: 0, text: "A, reworded", included: false },
      { index: 2, text: "C, reworded", included: true },
    ]);
  });

  // The exact collision scenario the plan's reorder-semantics rule exists to prevent: a second
  // reorder must not leave a moved bullet's explicit order colliding with an untouched bullet
  // that quietly reverted to looking "natural" after the first move touched everything once.
  it("stays correct across two consecutive reorders", () => {
    const afterFirst = moveBulletOverride(BASE, [], 2, -1); // C ahead of B -> A, C, B
    expect(orderedBulletRows(BASE, afterFirst).map(r => r.index)).toEqual([0, 2, 1]);

    const afterSecond = moveBulletOverride(BASE, afterFirst, 0, 1); // A moves down one -> C, A, B
    expect(orderedBulletRows(BASE, afterSecond).map(r => r.index)).toEqual([2, 0, 1]);
    expect(afterSecond.every(o => o.order !== null)).toBe(true);
  });

  it("does not mutate the input overrides array", () => {
    const overrides: ItemOverride[] = [{ index: 0, included: true, textOverride: null, order: null }];
    const copy = overrides.map(o => ({ ...o }));
    moveBulletOverride(BASE, overrides, 1, -1);
    expect(overrides).toEqual(copy);
  });
});

describe("upsertItemOverride", () => {
  it("creates a new entry with sensible defaults when none exists", () => {
    const result = upsertItemOverride([], 0, { textOverride: "New text" });
    expect(result).toEqual([{ index: 0, included: true, textOverride: "New text", order: null }]);
  });

  it("patches an existing entry without disturbing its other fields", () => {
    const overrides: ItemOverride[] = [{ index: 0, included: true, textOverride: "Old", order: 3 }];
    const result = upsertItemOverride(overrides, 0, { included: false });
    expect(result).toEqual([{ index: 0, included: false, textOverride: "Old", order: 3 }]);
  });

  it("does not disturb other indices", () => {
    const overrides: ItemOverride[] = [{ index: 1, included: true, textOverride: null, order: null }];
    const result = upsertItemOverride(overrides, 0, { included: false });
    expect(result.find(o => o.index === 1)).toEqual(overrides[0]);
  });
});
