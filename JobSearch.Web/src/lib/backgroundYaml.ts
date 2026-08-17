import { load as loadYaml, dump as dumpYaml } from "js-yaml";

export interface PersonalInfo {
  name: string;
  email: string;
  phone?: string;
  location?: string;
  linkedin?: string;
  github?: string;
}

export interface ExperienceEntry {
  company: string;
  role: string;
  dates: { start: string; end: string };
  location: string;
  employment_type: string;
  achievements: string[];
  // Fields like company_description, stack, anchors, notes can exist on a hand-edited
  // profile (the intake parser never produces them). Not surfaced as editable fields here,
  // but preserved verbatim on save via serializeBackgroundYaml's merge — never dropped.
  [key: string]: unknown;
}

export interface EducationEntry {
  institution: string;
  degree: string;
  location: string;
  graduation_year: string | number;
  [key: string]: unknown;
}

export interface ProjectEntry {
  name: string;
  description: string;
  tech_stack?: string;
  [key: string]: unknown;
}

export interface BackgroundData {
  personal: PersonalInfo;
  experience: ExperienceEntry[];
  education: EducationEntry[];
  // Arbitrary grouping (e.g. "languages", "frameworks", or just "general") -> flat item list.
  skills: Record<string, string[]>;
  projects: ProjectEntry[];
  // Anything outside the five sections above (e.g. a hand-authored "narrative" section) —
  // round-tripped untouched, never rendered or edited as cards.
  extra: Record<string, unknown>;
}

const KNOWN_KEYS = ["personal", "experience", "education", "skills", "projects"];

function normalizeSkills(raw: unknown): Record<string, string[]> {
  if (raw === null || typeof raw !== "object") return {};
  const result: Record<string, string[]> = {};
  for (const [category, items] of Object.entries(raw as Record<string, unknown>)) {
    result[category] = Array.isArray(items) ? items.map(String) : [String(items)];
  }
  return result;
}

// Union rather than throwing — a background can be hand-edited (e.g. via the Advanced raw-YAML
// box, or predate this feature entirely) and end up with a syntax quirk strict YAML rejects
// even though it's perfectly fine as text handed to Claude, which is all the rest of this app
// ever did with it before now. Falling back to raw-text editing loses nothing; silently
// resetting to empty cards would.
export type BackgroundParseResult =
  | { ok: true; data: BackgroundData }
  | { ok: false; rawText: string };

export function parseBackgroundYaml(text: string): BackgroundParseResult {
  let raw: Record<string, unknown>;
  try {
    raw = (loadYaml(text) ?? {}) as Record<string, unknown>;
  } catch {
    return { ok: false, rawText: text };
  }

  const extra: Record<string, unknown> = {};
  for (const key of Object.keys(raw)) {
    if (!KNOWN_KEYS.includes(key)) extra[key] = raw[key];
  }
  return {
    ok: true,
    data: {
      personal: (raw.personal as PersonalInfo) ?? { name: "", email: "" },
      experience: (raw.experience as ExperienceEntry[]) ?? [],
      education: (raw.education as EducationEntry[]) ?? [],
      skills: normalizeSkills(raw.skills),
      projects: (raw.projects as ProjectEntry[]) ?? [],
      extra,
    },
  };
}

export function serializeBackgroundYaml(data: BackgroundData): string {
  const ordered = {
    personal: data.personal,
    experience: data.experience,
    education: data.education,
    skills: data.skills,
    projects: data.projects,
    ...data.extra,
  };
  return dumpYaml(ordered, { lineWidth: -1 });
}
