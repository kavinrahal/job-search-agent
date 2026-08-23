import type { ReactNode } from "react";
import type { JobCriteriaData, SkillDimension, Disqualifier } from "../lib/jobCriteriaYaml";
import { LABEL, INPUT, Field, TopicCard, EntryCard, AddButton, AdvancedSection } from "./CardEditor";
import { COUNTRIES, CURRENCIES, STATES_BY_COUNTRY } from "../lib/regionData";
import { InfoTooltip } from "./InfoTooltip";
import { ChoiceButtons } from "./ChoiceButtons";
import { getMissingCriteriaFields } from "../lib/criteriaCompleteness";

// Exported so CriteriaWizard.tsx's Employment type question reuses the exact same list rather
// than maintaining a second copy that could drift.
export const EMPLOYMENT_TYPES = ["full_time", "part_time", "contract", "casual"];
const SENIORITY_LEVELS = ["junior", "mid", "senior", "lead"];

function splitCsv(text: string): string[] {
  return text.split(",").map(s => s.trim()).filter(Boolean);
}

function selectedValues(e: React.ChangeEvent<HTMLSelectElement>): string {
  return Array.from(e.target.selectedOptions, o => o.value).join(", ");
}

// The four match tiers recur for every skill dimension the candidate defines — one
// generic mechanism for any profession's tools/skills/certifications, not a fixed list of
// software-specific categories. See the plan's scope decision on why there's no dedicated
// "Cloud platform"/"AI tooling" section.
function TieredMatchFields({ value, onChange }: { value: SkillDimension; onChange: (v: SkillDimension) => void }) {
  const set = <K extends keyof SkillDimension>(key: K, v: SkillDimension[K]) => onChange({ ...value, [key]: v });
  return (
    <div>
      <div className="mb-2 flex items-center text-xs text-gray-400 dark:text-gray-500">
        List the specific skills/tools that fall in each tier below.
        <InfoTooltip text="These four tiers control how closely a posting's requirements need to match. Strong/good match boost a posting's ranking; acceptable is neutral; excluded rules it out. Leave any tier blank if it doesn't apply." />
      </div>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <Field label="Strong match (comma-separated)" value={value.strongMatch} onChange={v => set("strongMatch", v)} />
        <Field label="Good match (comma-separated)" value={value.goodMatch} onChange={v => set("goodMatch", v)} />
        <Field label="Acceptable (comma-separated)" value={value.acceptable} onChange={v => set("acceptable", v)} />
        <Field label="Excluded (comma-separated)" value={value.excluded} onChange={v => set("excluded", v)} />
      </div>
    </div>
  );
}

function SkillDimensionsSection({ value, onChange, missing }: { value: SkillDimension[]; onChange: (v: SkillDimension[]) => void; missing?: boolean }) {
  const update = (i: number, patch: Partial<SkillDimension>) =>
    onChange(value.map((d, idx) => (idx === i ? { ...d, ...patch } : d)));
  const remove = (i: number) => onChange(value.filter((_, idx) => idx !== i));
  const add = () => onChange([...value, {
    name: "", priority: "", strongMatch: "", goodMatch: "", acceptable: "", excluded: "", notes: "",
  }]);

  return (
    <TopicCard title="Skill dimensions" defaultOpen={false}>
      <p className="text-xs text-gray-400 dark:text-gray-500">
        Any skill, tool, certification, or knowledge area worth ranking candidates on. One
        entry per dimension, in priority order. Works for any profession: "Cloud platform"
        for an engineer, "EHR system experience" for a nurse, "Knife skills" for a chef.
      </p>
      <div className="space-y-3">
        {value.map((dim, i) => (
          <EntryCard key={i} summary={dim.name} onRemove={() => remove(i)}>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <Field label="Name" value={dim.name} onChange={v => update(i, { name: v })} />
              <Field
                label="Priority (1 = most important)"
                type="number"
                min={1}
                value={dim.priority}
                onChange={v => update(i, { priority: v })}
                tooltip="Lower numbers matter more when ranking a posting. If two dimensions matter equally, give them the same number."
              />
            </div>
            <TieredMatchFields value={dim} onChange={v => update(i, v)} />
            <Field label="Notes" value={dim.notes} onChange={v => update(i, { notes: v })} multiline />
          </EntryCard>
        ))}
        <AddButton onClick={add}>+ Add skill dimension</AddButton>
      </div>
      {missing && <RequiredWarning>Required — add at least one skill dimension with a name and a strong match.</RequiredWarning>}
    </TopicCard>
  );
}

function DisqualifiersSection({ value, onChange }: { value: Disqualifier[]; onChange: (v: Disqualifier[]) => void }) {
  const update = (i: number, patch: Partial<Disqualifier>) =>
    onChange(value.map((d, idx) => (idx === i ? { ...d, ...patch } : d)));
  const remove = (i: number) => onChange(value.filter((_, idx) => idx !== i));
  const add = () => onChange([...value, { id: "", description: "", signals: "", notes: "" }]);

  return (
    <TopicCard title="Disqualifiers">
      <p className="text-xs text-gray-400 dark:text-gray-500">
        Anything that should end evaluation immediately. Signals are the exact phrases to
        watch for in a posting (one per line). Optional, a description alone is enough.
        <InfoTooltip text="Disqualifiers rule a posting out entirely. Orange flags and FYI context (below) don't rule anything out, they just add a note to the evaluation." />
      </p>
      <div className="space-y-3">
        {value.map((dq, i) => (
          <EntryCard key={i} summary={dq.description} onRemove={() => remove(i)}>
            <Field label="Description" value={dq.description} onChange={v => update(i, { description: v })} />
            <Field label="Signal phrases (one per line)" value={dq.signals} onChange={v => update(i, { signals: v })} multiline />
            <Field label="Notes" value={dq.notes} onChange={v => update(i, { notes: v })} multiline />
          </EntryCard>
        ))}
        <AddButton onClick={add}>+ Add disqualifier</AddButton>
      </div>
    </TopicCard>
  );
}

// Matches the existing Target-job-titles warning's exact styling — one visual language for
// "this is required and currently blank" everywhere it appears, in both this editor and
// (via getMissingCriteriaFields) the wizard/dashboard banner.
function RequiredWarning({ children }: { children: ReactNode }) {
  return <p className="text-xs text-amber-600 dark:text-amber-400">{children}</p>;
}

export function JobCriteriaEditor({ value, onChange, tier }: { value: JobCriteriaData; onChange: (v: JobCriteriaData) => void; tier: string }) {
  const set = <K extends keyof JobCriteriaData>(key: K, v: JobCriteriaData[K]) => onChange({ ...value, [key]: v });

  // Only show a states multi-select when every currently-selected country has a known list —
  // for anything else (including no selection) free text is the only sane fallback.
  const selectedCountries = splitCsv(value.countries);
  const knownStates = selectedCountries.length > 0 && selectedCountries.every(c => c in STATES_BY_COUNTRY)
    ? Array.from(new Set(selectedCountries.flatMap(c => STATES_BY_COUNTRY[c])))
    : null;

  // Same check the wizard and the dashboard nudge use — one definition of "complete" everywhere,
  // so this editor can never disagree with either about what's still missing.
  const missing = getMissingCriteriaFields(value, tier);
  const isMissing = (key: string) => missing.some(m => m.key === key);

  return (
    <div className="space-y-4">
      {/* Tier2-only, since this specifically drives Tier2's automatic discovery — showing a
          "required" field for something that doesn't apply to Tier1 at all would just be
          confusing. Tier2's needsCriteria check (backend) knows about this field separately,
          so upgrading to Tier2 without ever filling it in correctly routes back through this
          page rather than silently leaving discovery broken. */}
      {tier === "Tier2" && (
        <TopicCard title="Target job titles">
          <p className="text-xs text-gray-400 dark:text-gray-500">
            The exact job titles to search for automatically — e.g. "Software Engineer, Backend
            Developer" or "Sous Chef, Line Cook". This is what actually gets searched; everything
            else below only affects how a found posting gets ranked.
            <InfoTooltip text="Required for automatic discovery to run. Without at least one title here, there's nothing to search for, so automatic discovery is skipped entirely until this is filled in." />
          </p>
          <Field
            label="Job titles (comma-separated) *"
            value={value.targetJobTitles}
            onChange={v => set("targetJobTitles", v)}
          />
          {isMissing("targetJobTitles") && (
            <RequiredWarning>Required — automatic discovery won't run until at least one title is added here.</RequiredWarning>
          )}
        </TopicCard>
      )}

      <TopicCard title="Employment type">
        <ChoiceButtons
          multi
          options={EMPLOYMENT_TYPES.map(type => ({ value: type, label: type.replace("_", " ") }))}
          value={value.employmentTypes}
          onChange={v => set("employmentTypes", v)}
        />
        {isMissing("employmentTypes") && <RequiredWarning>Required — select at least one.</RequiredWarning>}
      </TopicCard>

      <TopicCard title="Location">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <div>
            <label className={LABEL}>Countries you're eligible/willing to work in *</label>
            <select
              multiple
              className={`${INPUT} h-32`}
              value={selectedCountries}
              onChange={e => set("countries", selectedValues(e))}
            >
              {COUNTRIES.map(c => <option key={c} value={c}>{c}</option>)}
            </select>
            {isMissing("location") && <RequiredWarning>Required — select at least one country.</RequiredWarning>}
          </div>
          <div>
            <label className={LABEL}>States/regions (optional)</label>
            {knownStates ? (
              <select
                multiple
                className={`${INPUT} h-32`}
                value={splitCsv(value.states)}
                onChange={e => set("states", selectedValues(e))}
              >
                {knownStates.map(s => <option key={s} value={s}>{s}</option>)}
              </select>
            ) : (
              <input
                className={INPUT}
                placeholder="No declared states for the selected country, type freely"
                value={value.states}
                onChange={e => set("states", e.target.value)}
              />
            )}
          </div>
        </div>
        <Field label="Notes (e.g. city preference, or lack thereof)" value={value.locationNotes} onChange={v => set("locationNotes", v)} />
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <div>
            <label className="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-300">
              <input type="checkbox" checked={value.remoteAccepted} onChange={e => set("remoteAccepted", e.target.checked)} />
              Remote
            </label>
            <Field label="Condition" value={value.remoteCondition} onChange={v => set("remoteCondition", v)} />
          </div>
          <div>
            <label className="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-300">
              <input type="checkbox" checked={value.hybridAccepted} onChange={e => set("hybridAccepted", e.target.checked)} />
              Hybrid
            </label>
            <Field label="Notes" value={value.hybridNotes} onChange={v => set("hybridNotes", v)} />
          </div>
          <div>
            <label className="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-300">
              <input type="checkbox" checked={value.onsiteAccepted} onChange={e => set("onsiteAccepted", e.target.checked)} />
              On-site
            </label>
            <Field label="Notes" value={value.onsiteNotes} onChange={v => set("onsiteNotes", v)} />
          </div>
        </div>
        {isMissing("arrangement") && <RequiredWarning>Required — check at least one of remote, hybrid, or on-site.</RequiredWarning>}
      </TopicCard>

      <TopicCard title="Sponsorship" defaultOpen={false}>
        <p className="text-xs text-gray-400 dark:text-gray-500">
          If you need visa/work-authorization sponsorship, use this to describe when a
          posting should be disqualified for excluding sponsorship-needing candidates.
          Leave blank if this doesn't apply to you.
        </p>
        <Field label={'Model (e.g. "binary")'} value={value.sponsorshipModel} onChange={v => set("sponsorshipModel", v)} />
        <Field label="Discard when" value={value.sponsorshipDiscardDescription} onChange={v => set("sponsorshipDiscardDescription", v)} />
        <Field label="Example excluding phrases (one per line)" value={value.sponsorshipDiscardExamples} onChange={v => set("sponsorshipDiscardExamples", v)} multiline />
        <Field label="Treat as in-scope when (one per line)" value={value.sponsorshipInScope} onChange={v => set("sponsorshipInScope", v)} multiline />
        <Field label="Notes" value={value.sponsorshipNotes} onChange={v => set("sponsorshipNotes", v)} multiline />
      </TopicCard>

      <TopicCard title="Experience">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <div>
            <label className={LABEL}>Seniority level</label>
            <select className={INPUT} value={value.seniorityLevel} onChange={e => set("seniorityLevel", e.target.value)}>
              {SENIORITY_LEVELS.map(l => <option key={l} value={l}>{l}</option>)}
            </select>
          </div>
          <div>
            <Field label={'Your current experience (e.g. "~4 years") *'} value={value.candidateCurrentExperience} onChange={v => set("candidateCurrentExperience", v)} />
            {isMissing("experience") && <RequiredWarning>Required.</RequiredWarning>}
          </div>
        </div>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <Field label="Ideal (years required, up to)" type="number" min={0} value={value.idealMaxYears} onChange={v => set("idealMaxYears", v)} />
          <Field label="Acceptable (years required, min)" type="number" min={0} value={value.acceptableMinYears} onChange={v => set("acceptableMinYears", v)} />
          <Field label="Acceptable (years required, max)" type="number" min={0} value={value.acceptableMaxYears} onChange={v => set("acceptableMaxYears", v)} />
          <Field label="Excluded (years required, min)" type="number" min={0} value={value.excludedMinYears} onChange={v => set("excludedMinYears", v)} />
        </div>
        <Field label={'How to read a stated range (e.g. "evaluate the midpoint")'} value={value.whenRangeStatedNotes} onChange={v => set("whenRangeStatedNotes", v)} multiline />
        <Field label="How scope maps to seniority regardless of title" value={value.scopeOverTitleNotes} onChange={v => set("scopeOverTitleNotes", v)} multiline />
      </TopicCard>

      <TopicCard title="Salary">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <div>
            <label className={LABEL}>Currency</label>
            <select className={INPUT} value={value.currency} onChange={e => set("currency", e.target.value)}>
              {CURRENCIES.map(c => <option key={c.code} value={c.code}>{c.label}</option>)}
            </select>
          </div>
          <Field label="Flag if below" type="number" min={0} value={value.salaryFlagBelow} onChange={v => set("salaryFlagBelow", v)} />
          <Field label="Minimum acceptable" type="number" min={0} value={value.salaryMin} onChange={v => set("salaryMin", v)} />
          <Field label="Target range (low)" type="number" min={0} value={value.salaryTargetMin} onChange={v => set("salaryTargetMin", v)} />
          <Field label="Target range (high)" type="number" min={0} value={value.salaryMax} onChange={v => set("salaryMax", v)} />
          <Field label="Flag if above" type="number" min={0} value={value.salaryFlagAbove} onChange={v => set("salaryFlagAbove", v)} />
        </div>
        {isMissing("salary") && <RequiredWarning>Required — fill in at least one of minimum acceptable, target range, or target range (low).</RequiredWarning>}
        <Field label="Why flag below minimum" value={value.salaryBelowMinNote} onChange={v => set("salaryBelowMinNote", v)} multiline />
        <Field label="Why flag above range" value={value.salaryAboveMaxNote} onChange={v => set("salaryAboveMaxNote", v)} multiline />
        <Field label="When salary isn't stated" value={value.salaryMissingNote} onChange={v => set("salaryMissingNote", v)} multiline />
      </TopicCard>

      <SkillDimensionsSection value={value.skillDimensions} onChange={v => set("skillDimensions", v)} missing={isMissing("skillDimensions")} />

      <DisqualifiersSection value={value.disqualifiers} onChange={v => set("disqualifiers", v)} />

      <TopicCard title="Company" defaultOpen={false}>
        <Field label="Context (why company assessment matters to you)" value={value.companyContext} onChange={v => set("companyContext", v)} multiline />
        <Field label="Preferred (one per line)" value={value.companyPreferred} onChange={v => set("companyPreferred", v)} multiline />
        <Field label="Acceptable (one per line)" value={value.companyAcceptable} onChange={v => set("companyAcceptable", v)} multiline />
        <Field label="Weaker (one per line)" value={value.companyWeaker} onChange={v => set("companyWeaker", v)} multiline />
        <Field label="Excluded industries (one per line)" value={value.excludedIndustries} onChange={v => set("excludedIndustries", v)} multiline />
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Field label="Positive stability signals (one per line)" value={value.stabilityPositive} onChange={v => set("stabilityPositive", v)} multiline />
          <Field label="Concerning stability signals (one per line)" value={value.stabilityConcerning} onChange={v => set("stabilityConcerning", v)} multiline />
        </div>
        <Field label="How to weigh stability signals" value={value.stabilityApproach} onChange={v => set("stabilityApproach", v)} multiline />
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Field label="Positive culture signals (one per line)" value={value.culturePositive} onChange={v => set("culturePositive", v)} multiline />
          <Field label="Negative culture signals (one per line)" value={value.cultureNegative} onChange={v => set("cultureNegative", v)} multiline />
        </div>
        <Field label="External research: why it's worth checking" value={value.externalEnrichmentPurpose} onChange={v => set("externalEnrichmentPurpose", v)} />
        <Field label="External research sources (one per line)" value={value.externalEnrichmentSources} onChange={v => set("externalEnrichmentSources", v)} multiline />
        <Field label="External research notes" value={value.externalEnrichmentNotes} onChange={v => set("externalEnrichmentNotes", v)} multiline />
      </TopicCard>

      <TopicCard title="Role type" defaultOpen={false}>
        <Field label="Preferred (one per line)" value={value.roleTypePreferred} onChange={v => set("roleTypePreferred", v)} multiline />
        <Field label="Acceptable (one per line)" value={value.roleTypeAcceptable} onChange={v => set("roleTypeAcceptable", v)} multiline />
        <Field label="Weaker (one per line)" value={value.roleTypeWeaker} onChange={v => set("roleTypeWeaker", v)} multiline />
        <Field label="Excluded (one per line)" value={value.roleTypeExcluded} onChange={v => set("roleTypeExcluded", v)} multiline />
      </TopicCard>

      <TopicCard title="Team" defaultOpen={false}>
        <Field label="Minimum team size" type="number" min={0} value={value.minimumTeamSize} onChange={v => set("minimumTeamSize", v)} />
        <label className="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-300">
          <input type="checkbox" checked={value.onCallAccepted} onChange={e => set("onCallAccepted", e.target.checked)} />
          On-call is acceptable
        </label>
        <Field label="Condition (e.g. must be compensated)" value={value.onCallCondition} onChange={v => set("onCallCondition", v)} />
        <Field label="What to do if compensation isn't mentioned" value={value.onCallFlagIfMissing} onChange={v => set("onCallFlagIfMissing", v)} />
      </TopicCard>

      <TopicCard title="Orange flags" defaultOpen={false}>
        <p className="text-xs text-gray-400 dark:text-gray-500">
          Things worth surfacing alongside a recommendation, without disqualifying it. One
          per line.
          <InfoTooltip text="A negative-leaning note (e.g. a possible downside), unlike FYI context below which is neutral." />
        </p>
        <Field label="Orange flags (one per line)" value={value.orangeFlags} onChange={v => set("orangeFlags", v)} multiline />
      </TopicCard>

      <TopicCard title="FYI context" defaultOpen={false}>
        <p className="text-xs text-gray-400 dark:text-gray-500">
          Worth mentioning but not a flag or a disqualifier. One per line.
          <InfoTooltip text="A neutral note worth knowing, not a downside. Use Orange flags above for anything negative-leaning." />
        </p>
        <Field label="FYI context (one per line)" value={value.fyiContext} onChange={v => set("fyiContext", v)} multiline />
      </TopicCard>

      <AdvancedSection value={value.extra} onChange={v => set("extra", v)} />
    </div>
  );
}
