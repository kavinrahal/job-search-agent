import { describe, it, expect } from "vitest";
import { moveSection, toggleSectionIncluded, sectionLabel } from "./resumeSections";
import type { SectionConfigEntry } from "../types";

const SECTIONS: SectionConfigEntry[] = [
  { sectionKey: "experience", included: true },
  { sectionKey: "education", included: true },
  { sectionKey: "skills", included: false },
];

describe("moveSection", () => {
  it("swaps an entry with its previous neighbor when moving up", () => {
    const result = moveSection(SECTIONS, 1, -1);
    expect(result.map(s => s.sectionKey)).toEqual(["education", "experience", "skills"]);
  });

  it("swaps an entry with its next neighbor when moving down", () => {
    const result = moveSection(SECTIONS, 0, 1);
    expect(result.map(s => s.sectionKey)).toEqual(["education", "experience", "skills"]);
  });

  it("is a no-op moving the first entry up", () => {
    const result = moveSection(SECTIONS, 0, -1);
    expect(result).toEqual(SECTIONS);
  });

  it("is a no-op moving the last entry down", () => {
    const result = moveSection(SECTIONS, SECTIONS.length - 1, 1);
    expect(result).toEqual(SECTIONS);
  });

  it("does not mutate the input array", () => {
    const copy = SECTIONS.map(s => ({ ...s }));
    moveSection(SECTIONS, 0, 1);
    expect(SECTIONS).toEqual(copy);
  });
});

describe("toggleSectionIncluded", () => {
  it("flips only the targeted entry's included flag", () => {
    const result = toggleSectionIncluded(SECTIONS, 2);
    expect(result[2]).toEqual({ sectionKey: "skills", included: true });
    expect(result[0]).toEqual(SECTIONS[0]);
    expect(result[1]).toEqual(SECTIONS[1]);
  });

  it("does not mutate the input array", () => {
    const copy = SECTIONS.map(s => ({ ...s }));
    toggleSectionIncluded(SECTIONS, 0);
    expect(SECTIONS).toEqual(copy);
  });
});

describe("sectionLabel", () => {
  it("returns a human label for every known section key", () => {
    for (const key of ["experience", "education", "skills", "projects", "credentials", "publications", "volunteering"]) {
      expect(sectionLabel(key)).not.toBe(key);
    }
  });

  it("falls back to the raw key for an unknown section", () => {
    expect(sectionLabel("something_new")).toBe("something_new");
  });
});
