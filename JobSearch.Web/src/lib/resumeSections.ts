import type { SectionConfigEntry } from "../types";

// Pure, framework-free logic behind the resume builder's section list — kept separate from
// ResumeBuilder.tsx so reorder/toggle behaviour is pinned by a plain unit test instead of being
// buried in JSX (same split as criteriaWizardMapping.ts).

// Human labels for the fixed set of section keys ResumeRenderer actually knows how to render
// (see JobSearch.Data/ResumeOverrideSchema.cs's section_key enum) — display only, never stored.
export const SECTION_LABELS: Record<string, string> = {
  experience: "Experience",
  education: "Education",
  skills: "Skills",
  projects: "Projects",
  credentials: "Credentials",
  publications: "Publications",
  volunteering: "Volunteering & Leadership",
};

// key is one of the fixed section_key values (see the enum reference above), and the ?? key
// fallback means any other value is harmless anyway — typed Record lookup, not user input.
export function sectionLabel(key: string): string {
  // eslint-disable-next-line security/detect-object-injection
  return SECTION_LABELS[key] ?? key;
}

// Swaps the entry at `index` with its neighbor in `direction` (-1 = up, +1 = down). No-op past
// either end — callers disable the button at the boundary rather than relying on this to clamp.
export function moveSection(sections: SectionConfigEntry[], index: number, direction: -1 | 1): SectionConfigEntry[] {
  const target = index + direction;
  if (target < 0 || target >= sections.length) return sections;
  const next = [...sections];
  // index and target are both bounds-checked against sections.length above — plain array swap.
  // eslint-disable-next-line security/detect-object-injection
  [next[index], next[target]] = [next[target], next[index]];
  return next;
}

export function toggleSectionIncluded(sections: SectionConfigEntry[], index: number): SectionConfigEntry[] {
  return sections.map((s, i) => (i === index ? { ...s, included: !s.included } : s));
}
