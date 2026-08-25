import { describe, it, expect } from "vitest";
import { moveSection, toggleSectionIncluded, sectionLabel, applyTemplateToDraft } from "./resumeSections";
import type { ResumeData, SectionConfigEntry } from "../types";

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

describe("applyTemplateToDraft", () => {
  // Regression test for: applying an industry template wiped unsaved Summary/Experience/
  // Project/Skills edits because the handler wrote the server's full ResumeData response
  // straight into draft state. The server only ever rewrites sectionConfig, so the merge
  // should take sectionConfig (and updatedAt) from the response but keep every other field
  // from whatever the user was already editing.
  const draftInProgress: ResumeData = {
    summary: "Unsaved summary edit in progress",
    sectionConfig: [
      { sectionKey: "experience", included: true },
      { sectionKey: "skills", included: true },
    ],
    experienceOverrides: [
      {
        experienceIndex: 0,
        included: true,
        companyDescriptionOverride: null,
        achievements: [{ index: 0, included: true, textOverride: "Unsaved bullet edit", order: null }],
        extraAchievements: [],
        notes: null,
      },
    ],
    projectOverrides: [],
    skillsSection: [{ label: "Unsaved skill group", items: ["React"] }],
    updatedAt: "2026-08-01T00:00:00Z",
  };

  const templateResponse: ResumeData = {
    summary: "Last saved summary",
    sectionConfig: [
      { sectionKey: "skills", included: true },
      { sectionKey: "experience", included: true },
      { sectionKey: "education", included: true },
    ],
    experienceOverrides: [],
    projectOverrides: [],
    skillsSection: [],
    updatedAt: "2026-08-25T12:00:00Z",
  };

  it("takes sectionConfig and updatedAt from the template response", () => {
    const result = applyTemplateToDraft(draftInProgress, templateResponse);
    expect(result.sectionConfig).toEqual(templateResponse.sectionConfig);
    expect(result.updatedAt).toEqual(templateResponse.updatedAt);
  });

  it("preserves the in-progress draft's unsaved summary, overrides, and skills", () => {
    const result = applyTemplateToDraft(draftInProgress, templateResponse);
    expect(result.summary).toBe(draftInProgress.summary);
    expect(result.experienceOverrides).toBe(draftInProgress.experienceOverrides);
    expect(result.projectOverrides).toBe(draftInProgress.projectOverrides);
    expect(result.skillsSection).toBe(draftInProgress.skillsSection);
  });

  it("does not mutate either input", () => {
    const draftCopy = structuredClone(draftInProgress);
    const responseCopy = structuredClone(templateResponse);
    applyTemplateToDraft(draftInProgress, templateResponse);
    expect(draftInProgress).toEqual(draftCopy);
    expect(templateResponse).toEqual(responseCopy);
  });
});
