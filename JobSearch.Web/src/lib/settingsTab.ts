// The three local tabs SettingsPage renders itself (Account/Resume/Billing — see SUB_NAV_ITEMS's
// own comment on why Criteria/Sources/Help aren't among them). Pulled out of SettingsPage.tsx so
// this validation can be unit-tested without dragging in that page's whole component tree
// (BackgroundEditor, ResumePdfViewer/react-pdf, etc.).
export type SettingsTab = "account" | "resume" | "billing";

const SETTINGS_TABS: SettingsTab[] = ["account", "resume", "billing"];

// Guards the ?tab= query param SettingsShell navigates back with when a local-tab item is
// clicked from /criteria, /sources or /help (see SettingsShell's own comment) — untrusted input,
// not guaranteed to be one of the three real tabs.
export function isSettingsTab(value: string | null): value is SettingsTab {
  return value !== null && (SETTINGS_TABS as string[]).includes(value);
}
