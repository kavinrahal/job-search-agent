import { load as loadYaml, dump as dumpYaml } from "js-yaml";

// Repeatable object lists get real array state (mirrors backgroundYaml.ts's
// ExperienceEntry/EducationEntry) — everything else stays a delimited string edited via a
// single Field, matching this file's existing convention (skills, disqualifiers notes,
// etc. were already strings before this expansion).
export interface SkillDimension {
  name: string;
  priority: string;
  strongMatch: string;
  goodMatch: string;
  acceptable: string;
  excluded: string;
  notes: string;
}

export interface Disqualifier {
  id: string;
  description: string;
  signals: string;
  notes: string;
}

export interface JobCriteriaData {
  // Exactly what to search job boards for (Adzuna) — required for automatic discovery to
  // run at all. Deliberately a plain user-typed list, not something inferred from the rest
  // of criteria: an earlier version tried deriving this with AI from looser criteria, which
  // risked searching for the wrong thing whenever criteria was thin or incomplete.
  targetJobTitles: string;

  employmentTypes: string[];
  countries: string;
  states: string;
  locationNotes: string;
  remoteAccepted: boolean;
  remoteCondition: string;
  hybridAccepted: boolean;
  hybridNotes: string;
  onsiteAccepted: boolean;
  onsiteNotes: string;

  sponsorshipModel: string;
  sponsorshipDiscardDescription: string;
  sponsorshipDiscardExamples: string;
  sponsorshipInScope: string;
  sponsorshipNotes: string;

  seniorityLevel: string;
  candidateCurrentExperience: string;
  idealMaxYears: string;
  acceptableMinYears: string;
  acceptableMaxYears: string;
  excludedMinYears: string;
  whenRangeStatedNotes: string;
  scopeOverTitleNotes: string;

  currency: string;
  salaryMin: string;
  salaryMax: string;
  salaryTargetMin: string;
  salaryFlagBelow: string;
  salaryFlagAbove: string;
  salaryBelowMinNote: string;
  salaryAboveMaxNote: string;
  salaryMissingNote: string;

  skillDimensions: SkillDimension[];

  companyContext: string;
  companyPreferred: string;
  companyAcceptable: string;
  companyWeaker: string;
  stabilityPositive: string;
  stabilityConcerning: string;
  stabilityApproach: string;
  externalEnrichmentPurpose: string;
  externalEnrichmentSources: string;
  externalEnrichmentNotes: string;
  excludedIndustries: string;
  culturePositive: string;
  cultureNegative: string;

  roleTypePreferred: string;
  roleTypeAcceptable: string;
  roleTypeWeaker: string;
  roleTypeExcluded: string;

  minimumTeamSize: string;
  onCallAccepted: boolean;
  onCallCondition: string;
  onCallFlagIfMissing: string;

  orangeFlags: string;
  fyiContext: string;

  disqualifiers: Disqualifier[];

  // Anything the fields above can't cleanly represent — preserved untouched rather than
  // flattened and potentially corrupted. See the parse* helpers below.
  extra: Record<string, unknown>;
}

const DEFAULTS: Omit<JobCriteriaData, "extra"> = {
  targetJobTitles: "",

  employmentTypes: ["full_time"],
  countries: "",
  states: "",
  locationNotes: "",
  remoteAccepted: true,
  remoteCondition: "",
  hybridAccepted: true,
  hybridNotes: "",
  onsiteAccepted: true,
  onsiteNotes: "",

  sponsorshipModel: "",
  sponsorshipDiscardDescription: "",
  sponsorshipDiscardExamples: "",
  sponsorshipInScope: "",
  sponsorshipNotes: "",

  seniorityLevel: "mid",
  candidateCurrentExperience: "",
  idealMaxYears: "",
  acceptableMinYears: "",
  acceptableMaxYears: "",
  excludedMinYears: "",
  whenRangeStatedNotes: "",
  scopeOverTitleNotes: "",

  currency: "AUD",
  salaryMin: "",
  salaryMax: "",
  salaryTargetMin: "",
  salaryFlagBelow: "",
  salaryFlagAbove: "",
  salaryBelowMinNote: "",
  salaryAboveMaxNote: "",
  salaryMissingNote: "",

  skillDimensions: [],

  companyContext: "",
  companyPreferred: "",
  companyAcceptable: "",
  companyWeaker: "",
  stabilityPositive: "",
  stabilityConcerning: "",
  stabilityApproach: "",
  externalEnrichmentPurpose: "",
  externalEnrichmentSources: "",
  externalEnrichmentNotes: "",
  excludedIndustries: "",
  culturePositive: "",
  cultureNegative: "",

  roleTypePreferred: "",
  roleTypeAcceptable: "",
  roleTypeWeaker: "",
  roleTypeExcluded: "",

  minimumTeamSize: "",
  onCallAccepted: true,
  onCallCondition: "",
  onCallFlagIfMissing: "",

  orangeFlags: "",
  fyiContext: "",

  disqualifiers: [],
};

function isStringArray(v: unknown): v is string[] {
  return Array.isArray(v) && v.every(x => typeof x === "string");
}

function isPlainObject(v: unknown): v is Record<string, unknown> {
  return v !== null && typeof v === "object" && !Array.isArray(v);
}

// True only if every key present is one the simple form actually understands — a section
// with any extra/richer keys is left alone entirely rather than partially represented and
// silently narrowed on save.
function isCleanMatch(obj: unknown, knownKeys: string[]): obj is Record<string, unknown> {
  return isPlainObject(obj) && Object.keys(obj).every(k => knownKeys.includes(k));
}

function str(v: unknown): string {
  return v == null ? "" : String(v);
}

// YAML doesn't parse underscore-grouped numbers (e.g. "120_000") as numbers — they come
// through as plain strings. Strip separators some authors use for readability so the value
// still displays correctly in a number input rather than showing blank.
function numStr(v: unknown): string {
  return v == null ? "" : String(v).replace(/[_,]/g, "");
}

function linesOrCsv(v: unknown, sep: string): string {
  return isStringArray(v) ? v.join(sep) : "";
}

// The two software-specific keys (cloud_platform, ai_tooling) fold into the generic
// skill-dimensions list rather than getting bespoke sections — see the plan's scope
// decision. Tolerates either the tiered-match shape or a plain {weight, notes} shape.
function parseTieredOrWeighted(section: unknown, label: string): SkillDimension | null {
  if (isCleanMatch(section, ["strong_match", "good_match", "acceptable", "excluded", "notes"])) {
    return {
      name: label, priority: "",
      strongMatch: linesOrCsv(section.strong_match, ", "),
      goodMatch: linesOrCsv(section.good_match, ", "),
      acceptable: linesOrCsv(section.acceptable, ", "),
      excluded: linesOrCsv(section.excluded, ", "),
      notes: typeof section.notes === "string" ? section.notes : "",
    };
  }
  if (isCleanMatch(section, ["weight", "notes"])) {
    const weight = section.weight != null ? `Weight: ${str(section.weight)}. ` : "";
    return {
      name: label, priority: "", strongMatch: "", goodMatch: "", acceptable: "", excluded: "",
      notes: weight + (typeof section.notes === "string" ? section.notes : ""),
    };
  }
  return null;
}

export function parseJobCriteriaYaml(text: string): JobCriteriaData {
  if (!text.trim()) return { ...DEFAULTS, extra: {} };

  let raw: Record<string, unknown>;
  try {
    raw = (loadYaml(text) ?? {}) as Record<string, unknown>;
  } catch {
    // Text that doesn't even parse as YAML (a hand-authored file with a syntax slip, say) —
    // keep it verbatim in extra rather than silently discarding it, so a save still shows
    // it via the Advanced (raw YAML) section instead of quietly overwriting it with blank
    // defaults the moment the form is saved.
    return { ...DEFAULTS, extra: { _unparsed_yaml: text } };
  }

  const extra: Record<string, unknown> = { ...raw };
  const data = { ...DEFAULTS };

  if (typeof raw.target_job_titles === "string") {
    data.targetJobTitles = raw.target_job_titles;
    delete extra.target_job_titles;
  } else if (isStringArray(raw.target_job_titles)) {
    // Tolerate a hand-edited YAML list too (Advanced section), not just the plain string
    // this editor itself writes.
    data.targetJobTitles = raw.target_job_titles.join(", ");
    delete extra.target_job_titles;
  }

  if (isStringArray(raw.employment_type_preference) && raw.employment_type_preference.length > 0) {
    data.employmentTypes = raw.employment_type_preference;
    delete extra.employment_type_preference;
  }

  const locationKeys = ["countries", "states", "preferred", "notes", "remote", "hybrid", "on_site"];
  if (isCleanMatch(raw.location, locationKeys)) {
    const l = raw.location;
    if (isStringArray(l.countries)) data.countries = l.countries.join(", ");
    // `preferred` is a list of { country: "..." } entries (or plain strings) in the richer
    // shape, rather than a flat `countries` array — either form is accepted.
    if (Array.isArray(l.preferred)) {
      const names = l.preferred
        .map(p => (typeof p === "string" ? p : isPlainObject(p) && typeof p.country === "string" ? p.country : null))
        .filter((n): n is string => !!n);
      if (names.length > 0) data.countries = names.join(", ");
    }
    if (isStringArray(l.states)) data.states = l.states.join(", ");
    if (typeof l.notes === "string") data.locationNotes = l.notes;
    if (isCleanMatch(l.remote, ["accepted", "condition"])) {
      if (typeof l.remote.accepted === "boolean") data.remoteAccepted = l.remote.accepted;
      if (typeof l.remote.condition === "string") data.remoteCondition = l.remote.condition;
    }
    if (isCleanMatch(l.hybrid, ["accepted", "notes"])) {
      if (typeof l.hybrid.accepted === "boolean") data.hybridAccepted = l.hybrid.accepted;
      if (typeof l.hybrid.notes === "string") data.hybridNotes = l.hybrid.notes;
    }
    if (isCleanMatch(l.on_site, ["accepted", "notes"])) {
      if (typeof l.on_site.accepted === "boolean") data.onsiteAccepted = l.on_site.accepted;
      if (typeof l.on_site.notes === "string") data.onsiteNotes = l.on_site.notes;
    } else if (isPlainObject(l.on_site)) {
      // Free-form single-key notes like { anywhere_in_australia: "acceptable" } — keep the
      // acceptance default (true) and surface the content as readable notes.
      data.onsiteNotes = Object.entries(l.on_site).map(([k, v]) => `${k}: ${str(v)}`).join("\n");
    }
    delete extra.location;
  } else {
    // Backward-compat: the old flat shape this editor used to write.
    const oldKeys = ["countries", "states", "remote_accepted", "hybrid_accepted", "onsite_accepted"];
    if (isCleanMatch(raw.location, oldKeys)) {
      const l = raw.location;
      if (isStringArray(l.countries)) data.countries = l.countries.join(", ");
      if (isStringArray(l.states)) data.states = l.states.join(", ");
      if (typeof l.remote_accepted === "boolean") data.remoteAccepted = l.remote_accepted;
      if (typeof l.hybrid_accepted === "boolean") data.hybridAccepted = l.hybrid_accepted;
      if (typeof l.onsite_accepted === "boolean") data.onsiteAccepted = l.onsite_accepted;
      delete extra.location;
    }
  }

  const sponsorshipKeys = ["model", "discard", "in_scope", "principle"];
  if (isCleanMatch(raw.sponsorship, sponsorshipKeys)) {
    const s = raw.sponsorship;
    if (typeof s.model === "string") data.sponsorshipModel = s.model;
    if (isCleanMatch(s.discard, ["description", "examples"])) {
      if (typeof s.discard.description === "string") data.sponsorshipDiscardDescription = s.discard.description;
      data.sponsorshipDiscardExamples = linesOrCsv(s.discard.examples, "\n");
    }
    data.sponsorshipInScope = linesOrCsv(s.in_scope, "\n");
    if (typeof s.principle === "string") data.sponsorshipNotes = s.principle;
    delete extra.sponsorship;
  }

  const experienceKeys = ["seniority_level", "candidate_current", "ranges", "when_range_stated", "scope_over_title"];
  if (isCleanMatch(raw.experience, experienceKeys)) {
    const e = raw.experience;
    if (typeof e.seniority_level === "string") data.seniorityLevel = e.seniority_level;
    if (typeof e.candidate_current === "string") data.candidateCurrentExperience = e.candidate_current;
    if (isPlainObject(e.ranges)) {
      const r = e.ranges;
      if (isPlainObject(r.ideal) && r.ideal.max_required != null) data.idealMaxYears = numStr(r.ideal.max_required);
      if (isPlainObject(r.acceptable)) {
        if (r.acceptable.min_required != null) data.acceptableMinYears = numStr(r.acceptable.min_required);
        if (r.acceptable.max_required != null) data.acceptableMaxYears = numStr(r.acceptable.max_required);
      }
      if (isPlainObject(r.excluded) && r.excluded.min_required != null) data.excludedMinYears = numStr(r.excluded.min_required);
    }
    if (typeof e.when_range_stated === "string") data.whenRangeStatedNotes = e.when_range_stated;
    if (typeof e.scope_over_title === "string") data.scopeOverTitleNotes = e.scope_over_title;
    delete extra.experience;
  } else if (isCleanMatch(raw.experience, ["seniority_level"])) {
    if (typeof raw.experience.seniority_level === "string") data.seniorityLevel = raw.experience.seniority_level;
    delete extra.experience;
  }

  const salaryKeys = ["currency", "target_base", "thresholds", "flag_reasons"];
  if (isCleanMatch(raw.salary, salaryKeys)) {
    const s = raw.salary;
    if (typeof s.currency === "string") data.currency = s.currency;
    let targetMin: string | null = s.target_base != null ? numStr(s.target_base) : null;
    if (isPlainObject(s.thresholds)) {
      const t = s.thresholds;
      if (t.flag_below != null) data.salaryFlagBelow = numStr(t.flag_below);
      if (t.acceptable_minimum != null) data.salaryMin = numStr(t.acceptable_minimum);
      if (t.flag_above != null) data.salaryFlagAbove = numStr(t.flag_above);
      if (Array.isArray(t.target_range) && t.target_range.length === 2) {
        targetMin = numStr(t.target_range[0]);
        data.salaryMax = numStr(t.target_range[1]);
      }
    }
    if (targetMin != null) data.salaryTargetMin = targetMin;
    if (isPlainObject(s.flag_reasons)) {
      for (const [key, val] of Object.entries(s.flag_reasons)) {
        if (typeof val !== "string") continue;
        if (key === "missing") data.salaryMissingNote = val;
        else if (/below|low|min/i.test(key)) data.salaryBelowMinNote = val;
        else if (/above|high|max/i.test(key)) data.salaryAboveMaxNote = val;
      }
    }
    delete extra.salary;
  } else if (isCleanMatch(raw.salary, ["currency", "minimum_acceptable", "target_max"])) {
    const s = raw.salary;
    if (typeof s.currency === "string") data.currency = s.currency;
    if (s.minimum_acceptable != null) data.salaryMin = numStr(s.minimum_acceptable);
    if (s.target_max != null) data.salaryMax = numStr(s.target_max);
    delete extra.salary;
  }

  // Skill dimensions: the rich per-dimension shape, the old single flat {name, keywords}
  // shape, and the owner file's two software-specific top-level keys (cloud_platform,
  // ai_tooling) all fold into the same repeatable list — see the plan's scope decision on
  // why there's no dedicated "Cloud platform"/"AI tooling" section.
  const dimensionKeys = ["name", "priority", "strong_match", "good_match", "acceptable", "excluded", "notes"];
  const dims = raw.skill_dimensions;
  if (Array.isArray(dims) && dims.length > 0 && dims.every(d => isCleanMatch(d, dimensionKeys))) {
    data.skillDimensions = (dims as Record<string, unknown>[]).map(d => ({
      name: str(d.name),
      priority: d.priority != null ? numStr(d.priority) : "",
      strongMatch: linesOrCsv(d.strong_match, ", "),
      goodMatch: linesOrCsv(d.good_match, ", "),
      acceptable: linesOrCsv(d.acceptable, ", "),
      excluded: linesOrCsv(d.excluded, ", "),
      notes: typeof d.notes === "string" ? d.notes : "",
    }));
    delete extra.skill_dimensions;
  } else if (Array.isArray(dims) && dims.length === 1 && isCleanMatch(dims[0], ["name", "keywords"]) && isStringArray(dims[0].keywords)) {
    data.skillDimensions = [{
      name: str(dims[0].name), priority: "", strongMatch: dims[0].keywords.join(", "),
      goodMatch: "", acceptable: "", excluded: "", notes: "",
    }];
    delete extra.skill_dimensions;
  }

  for (const [key, label] of [["cloud_platform", "Cloud platform"], ["ai_tooling", "AI tooling"]] as const) {
    const dim = parseTieredOrWeighted(raw[key], label);
    if (dim) {
      data.skillDimensions.push(dim);
      delete extra[key];
    }
  }

  const companyKeys = [
    "context", "preferred", "acceptable", "weaker", "stability_signals",
    "external_enrichment", "excluded_industries", "culture_signals",
  ];
  if (isCleanMatch(raw.company, companyKeys)) {
    const c = raw.company;
    if (typeof c.context === "string") data.companyContext = c.context;
    data.companyPreferred = linesOrCsv(c.preferred, "\n");
    data.companyAcceptable = linesOrCsv(c.acceptable, "\n");
    data.companyWeaker = linesOrCsv(c.weaker, "\n");
    if (isCleanMatch(c.stability_signals, ["positive", "concerning", "approach"])) {
      data.stabilityPositive = linesOrCsv(c.stability_signals.positive, "\n");
      data.stabilityConcerning = linesOrCsv(c.stability_signals.concerning, "\n");
      if (typeof c.stability_signals.approach === "string") data.stabilityApproach = c.stability_signals.approach;
    }
    if (isCleanMatch(c.external_enrichment, ["purpose", "sources", "notes"])) {
      const ee = c.external_enrichment;
      if (typeof ee.purpose === "string") data.externalEnrichmentPurpose = ee.purpose;
      if (typeof ee.notes === "string") data.externalEnrichmentNotes = ee.notes;
      // Each source is a single-key { name: description } object rather than a plain
      // string — render as "name: description" lines so nothing is lost.
      if (Array.isArray(ee.sources)) {
        data.externalEnrichmentSources = ee.sources
          .map(s => isPlainObject(s) ? Object.entries(s).map(([k, v]) => `${k}: ${str(v)}`).join("\n") : typeof s === "string" ? s : null)
          .filter((s): s is string => !!s)
          .join("\n");
      }
    } else if (isStringArray(c.external_enrichment)) {
      data.externalEnrichmentSources = c.external_enrichment.join("\n");
    }
    data.excludedIndustries = linesOrCsv(c.excluded_industries, "\n");
    if (isCleanMatch(c.culture_signals, ["positive", "negative"])) {
      data.culturePositive = linesOrCsv(c.culture_signals.positive, "\n");
      data.cultureNegative = linesOrCsv(c.culture_signals.negative, "\n");
    }
    delete extra.company;
  }

  const roleTypeKeys = ["preferred", "acceptable", "weaker", "excluded"];
  if (isCleanMatch(raw.role_type, roleTypeKeys)) {
    const r = raw.role_type;
    data.roleTypePreferred = linesOrCsv(r.preferred, "\n");
    data.roleTypeAcceptable = linesOrCsv(r.acceptable, "\n");
    data.roleTypeWeaker = linesOrCsv(r.weaker, "\n");
    data.roleTypeExcluded = linesOrCsv(r.excluded, "\n");
    delete extra.role_type;
  }

  // "minimum_engineers" is the legacy software-specific key name (still accepted here so
  // existing files load), "minimum_team_size" is what this editor writes going forward.
  const teamKeys = ["minimum_team_size", "minimum_engineers", "on_call"];
  if (isCleanMatch(raw.team, teamKeys)) {
    const t = raw.team;
    const size = t.minimum_team_size ?? t.minimum_engineers;
    if (size != null) data.minimumTeamSize = numStr(size);
    // "acceptable" is the legacy key name (job_criteria.yaml uses it), "accepted" matches
    // the naming used by remote/hybrid/on_site elsewhere in this schema.
    if (isCleanMatch(t.on_call, ["accepted", "acceptable", "condition", "flag_if_missing"])) {
      const accepted = t.on_call.accepted ?? t.on_call.acceptable;
      if (typeof accepted === "boolean") data.onCallAccepted = accepted;
      if (typeof t.on_call.condition === "string") data.onCallCondition = t.on_call.condition;
      if (typeof t.on_call.flag_if_missing === "string") data.onCallFlagIfMissing = t.on_call.flag_if_missing;
    }
    delete extra.team;
  }

  if (isStringArray(raw.orange_flags)) {
    data.orangeFlags = raw.orange_flags.join("\n");
    delete extra.orange_flags;
  }
  if (isStringArray(raw.fyi_context)) {
    data.fyiContext = raw.fyi_context.join("\n");
    delete extra.fyi_context;
  }

  const disqualifierKeys = ["id", "description", "signals", "notes"];
  const dqs = raw.hard_disqualifiers;
  if (Array.isArray(dqs) && dqs.length > 0 && dqs.every(d => isCleanMatch(d, disqualifierKeys))) {
    data.disqualifiers = (dqs as Record<string, unknown>[]).map(d => ({
      id: str(d.id), description: str(d.description),
      signals: linesOrCsv(d.signals, "\n"),
      notes: typeof d.notes === "string" ? d.notes : "",
    }));
    delete extra.hard_disqualifiers;
  } else if (isStringArray(dqs)) {
    data.disqualifiers = dqs.map(line => ({ id: "", description: line, signals: "", notes: "" }));
    delete extra.hard_disqualifiers;
  }

  if (isStringArray(raw.company_preferences)) {
    data.companyPreferred = [data.companyPreferred, raw.company_preferences.join("\n")].filter(Boolean).join("\n");
    delete extra.company_preferences;
  }
  if (isStringArray(raw.role_type_preferences)) {
    data.roleTypePreferred = [data.roleTypePreferred, raw.role_type_preferences.join("\n")].filter(Boolean).join("\n");
    delete extra.role_type_preferences;
  }

  return { ...data, extra };
}

function split(text: string, sep: string): string[] {
  return text.split(sep).map(s => s.trim()).filter(Boolean);
}

function optionalNumber(text: string): number | undefined {
  return text.trim() ? Number(text) : undefined;
}

// Anything preserved in `extra` wins over the simple fields on key collision — a section left
// untouched by parseJobCriteriaYaml (too rich to safely claim) stays exactly as it was, rather
// than being overwritten by the form's empty/default value for that same key.
export function serializeJobCriteriaYaml(data: JobCriteriaData): string {
  const fromForm: Record<string, unknown> = {
    target_job_titles: data.targetJobTitles,
    employment_type_preference: data.employmentTypes,
    location: {
      countries: split(data.countries, ","),
      states: split(data.states, ","),
      notes: data.locationNotes,
      remote: { accepted: data.remoteAccepted, condition: data.remoteCondition },
      hybrid: { accepted: data.hybridAccepted, notes: data.hybridNotes },
      on_site: { accepted: data.onsiteAccepted, notes: data.onsiteNotes },
    },
    sponsorship: {
      model: data.sponsorshipModel,
      discard: {
        description: data.sponsorshipDiscardDescription,
        examples: split(data.sponsorshipDiscardExamples, "\n"),
      },
      in_scope: split(data.sponsorshipInScope, "\n"),
      principle: data.sponsorshipNotes,
    },
    experience: {
      seniority_level: data.seniorityLevel,
      candidate_current: data.candidateCurrentExperience,
      ranges: {
        ideal: { max_required: optionalNumber(data.idealMaxYears) },
        acceptable: { min_required: optionalNumber(data.acceptableMinYears), max_required: optionalNumber(data.acceptableMaxYears) },
        excluded: { min_required: optionalNumber(data.excludedMinYears) },
      },
      when_range_stated: data.whenRangeStatedNotes,
      scope_over_title: data.scopeOverTitleNotes,
    },
    salary: {
      currency: data.currency,
      ...(data.salaryMin.trim() ? { minimum_acceptable: Number(data.salaryMin) } : {}),
      ...(data.salaryMax.trim() ? { target_max: Number(data.salaryMax) } : {}),
      thresholds: {
        ...(data.salaryFlagBelow.trim() ? { flag_below: Number(data.salaryFlagBelow) } : {}),
        ...(data.salaryMin.trim() ? { acceptable_minimum: Number(data.salaryMin) } : {}),
        ...(data.salaryTargetMin.trim() && data.salaryMax.trim()
          ? { target_range: [Number(data.salaryTargetMin), Number(data.salaryMax)] } : {}),
        ...(data.salaryFlagAbove.trim() ? { flag_above: Number(data.salaryFlagAbove) } : {}),
      },
      flag_reasons: {
        below_minimum: data.salaryBelowMinNote,
        above_maximum: data.salaryAboveMaxNote,
        missing: data.salaryMissingNote,
      },
    },
    skill_dimensions: data.skillDimensions.map(d => ({
      name: d.name,
      ...(d.priority.trim() ? { priority: Number(d.priority) } : {}),
      strong_match: split(d.strongMatch, ","),
      good_match: split(d.goodMatch, ","),
      acceptable: split(d.acceptable, ","),
      excluded: split(d.excluded, ","),
      notes: d.notes,
    })),
    company: {
      context: data.companyContext,
      preferred: split(data.companyPreferred, "\n"),
      acceptable: split(data.companyAcceptable, "\n"),
      weaker: split(data.companyWeaker, "\n"),
      stability_signals: {
        positive: split(data.stabilityPositive, "\n"),
        concerning: split(data.stabilityConcerning, "\n"),
        approach: data.stabilityApproach,
      },
      external_enrichment: {
        purpose: data.externalEnrichmentPurpose,
        sources: split(data.externalEnrichmentSources, "\n"),
        notes: data.externalEnrichmentNotes,
      },
      excluded_industries: split(data.excludedIndustries, "\n"),
      culture_signals: {
        positive: split(data.culturePositive, "\n"),
        negative: split(data.cultureNegative, "\n"),
      },
    },
    role_type: {
      preferred: split(data.roleTypePreferred, "\n"),
      acceptable: split(data.roleTypeAcceptable, "\n"),
      weaker: split(data.roleTypeWeaker, "\n"),
      excluded: split(data.roleTypeExcluded, "\n"),
    },
    team: {
      ...(data.minimumTeamSize.trim() ? { minimum_team_size: Number(data.minimumTeamSize) } : {}),
      on_call: {
        accepted: data.onCallAccepted,
        condition: data.onCallCondition,
        flag_if_missing: data.onCallFlagIfMissing,
      },
    },
    orange_flags: split(data.orangeFlags, "\n"),
    fyi_context: split(data.fyiContext, "\n"),
    hard_disqualifiers: data.disqualifiers.map(d => ({
      ...(d.id.trim() ? { id: d.id } : {}),
      description: d.description,
      signals: split(d.signals, "\n"),
      notes: d.notes,
    })),
  };
  return dumpYaml({ ...fromForm, ...data.extra }, { lineWidth: -1 });
}
