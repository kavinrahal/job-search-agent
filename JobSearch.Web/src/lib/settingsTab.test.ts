import { describe, it, expect } from "vitest";
import { isSettingsTab } from "./settingsTab";

// SettingsPage seeds its initial tab from ?tab=, the query param SettingsShell navigates back
// with when a local-tab item (Account/Resume/Billing) is clicked from /criteria, /sources or
// /help (see SettingsShell's own comment). isSettingsTab is the guard that keeps that seed
// trustworthy — a missing or unrecognised value must fall back to "account", not be trusted
// blindly from the query string.
describe("isSettingsTab", () => {
  it("accepts each of the three real tabs", () => {
    expect(isSettingsTab("account")).toBe(true);
    expect(isSettingsTab("resume")).toBe(true);
    expect(isSettingsTab("billing")).toBe(true);
  });

  it("rejects a missing param", () => {
    expect(isSettingsTab(null)).toBe(false);
  });

  it("rejects a value that isn't one of the three tabs", () => {
    expect(isSettingsTab("criteria")).toBe(false);
    expect(isSettingsTab("")).toBe(false);
    expect(isSettingsTab("Account")).toBe(false);
  });
});
