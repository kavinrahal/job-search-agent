import { load as loadYaml, dump as dumpYaml } from "js-yaml";

export interface JobCriteriaData {
  employmentTypes: string[];
  countries: string;
  states: string;
  remoteAccepted: boolean;
  hybridAccepted: boolean;
  onsiteAccepted: boolean;
  seniorityLevel: string;
  currency: string;
  salaryMin: string;
  salaryMax: string;
  skills: string;
  disqualifiers: string;
  companyPreferences: string;
  roleTypePreferences: string;
  // Anything the simple fields above can't cleanly represent (e.g. hard_disqualifiers with
  // per-item id/description/signals rather than plain strings, or per-skill-dimension tiers)
  // — preserved untouched rather than flattened and potentially corrupted. See parseSection.
  extra: Record<string, unknown>;
}

const DEFAULTS: Omit<JobCriteriaData, "extra"> = {
  employmentTypes: ["full_time"],
  countries: "",
  states: "",
  remoteAccepted: true,
  hybridAccepted: true,
  onsiteAccepted: true,
  seniorityLevel: "mid",
  currency: "AUD",
  salaryMin: "",
  salaryMax: "",
  skills: "",
  disqualifiers: "",
  companyPreferences: "",
  roleTypePreferences: "",
};

function isStringArray(v: unknown): v is string[] {
  return Array.isArray(v) && v.every(x => typeof x === "string");
}

// True only if every key present is one the simple form actually understands — a section
// with any extra/richer keys (e.g. per-disqualifier "signals", per-dimension "strong_match")
// is left alone entirely rather than partially represented and silently narrowed on save.
function isCleanMatch(obj: unknown, knownKeys: string[]): obj is Record<string, unknown> {
  return obj !== null && typeof obj === "object" && !Array.isArray(obj)
    && Object.keys(obj).every(k => knownKeys.includes(k));
}

export function parseJobCriteriaYaml(text: string): JobCriteriaData {
  if (!text.trim()) return { ...DEFAULTS, extra: {} };

  let raw: Record<string, unknown>;
  try {
    raw = (loadYaml(text) ?? {}) as Record<string, unknown>;
  } catch {
    return { ...DEFAULTS, extra: {} };
  }

  const extra: Record<string, unknown> = { ...raw };
  const data = { ...DEFAULTS };

  if (isStringArray(raw.employment_type_preference) && raw.employment_type_preference.length > 0) {
    data.employmentTypes = raw.employment_type_preference;
    delete extra.employment_type_preference;
  }

  const locationKeys = ["countries", "states", "remote_accepted", "hybrid_accepted", "onsite_accepted"];
  if (isCleanMatch(raw.location, locationKeys)) {
    const l = raw.location;
    if (isStringArray(l.countries)) data.countries = l.countries.join(", ");
    if (isStringArray(l.states)) data.states = l.states.join(", ");
    if (typeof l.remote_accepted === "boolean") data.remoteAccepted = l.remote_accepted;
    if (typeof l.hybrid_accepted === "boolean") data.hybridAccepted = l.hybrid_accepted;
    if (typeof l.onsite_accepted === "boolean") data.onsiteAccepted = l.onsite_accepted;
    delete extra.location;
  }

  if (isCleanMatch(raw.experience, ["seniority_level"])) {
    if (typeof raw.experience.seniority_level === "string") data.seniorityLevel = raw.experience.seniority_level;
    delete extra.experience;
  }

  const salaryKeys = ["currency", "minimum_acceptable", "target_max"];
  if (isCleanMatch(raw.salary, salaryKeys)) {
    const s = raw.salary;
    if (typeof s.currency === "string") data.currency = s.currency;
    if (s.minimum_acceptable != null) data.salaryMin = String(s.minimum_acceptable);
    if (s.target_max != null) data.salaryMax = String(s.target_max);
    delete extra.salary;
  }

  const dims = raw.skill_dimensions;
  if (Array.isArray(dims) && dims.length === 1 && isCleanMatch(dims[0], ["name", "keywords"]) && isStringArray(dims[0].keywords)) {
    data.skills = dims[0].keywords.join(", ");
    delete extra.skill_dimensions;
  }

  if (isStringArray(raw.hard_disqualifiers)) {
    data.disqualifiers = raw.hard_disqualifiers.join("\n");
    delete extra.hard_disqualifiers;
  }
  if (isStringArray(raw.company_preferences)) {
    data.companyPreferences = raw.company_preferences.join("\n");
    delete extra.company_preferences;
  }
  if (isStringArray(raw.role_type_preferences)) {
    data.roleTypePreferences = raw.role_type_preferences.join("\n");
    delete extra.role_type_preferences;
  }

  return { ...data, extra };
}

function split(text: string, sep: string): string[] {
  return text.split(sep).map(s => s.trim()).filter(Boolean);
}

// Anything preserved in `extra` wins over the simple fields on key collision — a section left
// untouched by parseJobCriteriaYaml (too rich to safely claim) stays exactly as it was, rather
// than being overwritten by the simple form's empty/default value for that same key.
export function serializeJobCriteriaYaml(data: JobCriteriaData): string {
  const fromForm: Record<string, unknown> = {
    employment_type_preference: data.employmentTypes,
    location: {
      countries: split(data.countries, ","),
      states: split(data.states, ","),
      remote_accepted: data.remoteAccepted,
      hybrid_accepted: data.hybridAccepted,
      onsite_accepted: data.onsiteAccepted,
    },
    experience: { seniority_level: data.seniorityLevel },
    salary: {
      currency: data.currency,
      ...(data.salaryMin.trim() ? { minimum_acceptable: Number(data.salaryMin) } : {}),
      ...(data.salaryMax.trim() ? { target_max: Number(data.salaryMax) } : {}),
    },
    skill_dimensions: [{ name: "Primary skills", keywords: split(data.skills, ",") }],
    hard_disqualifiers: split(data.disqualifiers, "\n"),
    company_preferences: split(data.companyPreferences, "\n"),
    role_type_preferences: split(data.roleTypePreferences, "\n"),
  };
  return dumpYaml({ ...fromForm, ...data.extra }, { lineWidth: -1 });
}
