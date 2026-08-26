import { describe, it, expect } from "vitest";
import { getMissingBackgroundFields, type BackgroundData } from "./backgroundYaml";

const COMPLETE: BackgroundData = {
  personal: { name: "Jane Doe", email: "jane@example.com" },
  experience: [],
  education: [],
  skills: {},
  projects: [],
  extra: {},
};

describe("getMissingBackgroundFields", () => {
  it("returns nothing when name and email are both filled", () => {
    expect(getMissingBackgroundFields(COMPLETE)).toEqual([]);
  });

  it("flags Name missing when blank or whitespace-only", () => {
    expect(getMissingBackgroundFields({ ...COMPLETE, personal: { ...COMPLETE.personal, name: "" } })).toContain("Name");
    expect(getMissingBackgroundFields({ ...COMPLETE, personal: { ...COMPLETE.personal, name: "   " } })).toContain("Name");
  });

  it("flags Email missing when blank or whitespace-only", () => {
    expect(getMissingBackgroundFields({ ...COMPLETE, personal: { ...COMPLETE.personal, email: "" } })).toContain("Email");
    expect(getMissingBackgroundFields({ ...COMPLETE, personal: { ...COMPLETE.personal, email: "   " } })).toContain("Email");
  });

  it("flags both when the empty-from-scratch starting shape is unchanged", () => {
    const empty: BackgroundData = { personal: { name: "", email: "" }, experience: [], education: [], skills: {}, projects: [], extra: {} };
    expect(getMissingBackgroundFields(empty)).toEqual(["Name", "Email"]);
  });
});
