import type { JobCriteriaData } from "../lib/jobCriteriaYaml";
import { LABEL, INPUT, Field, TopicCard, AdvancedSection } from "./CardEditor";

const EMPLOYMENT_TYPES = ["full_time", "part_time", "contract", "casual"];
const SENIORITY_LEVELS = ["junior", "mid", "senior", "lead"];

export function JobCriteriaEditor({ value, onChange }: { value: JobCriteriaData; onChange: (v: JobCriteriaData) => void }) {
  const set = <K extends keyof JobCriteriaData>(key: K, v: JobCriteriaData[K]) => onChange({ ...value, [key]: v });

  function toggleEmploymentType(type: string) {
    set("employmentTypes", value.employmentTypes.includes(type)
      ? value.employmentTypes.filter(t => t !== type)
      : [...value.employmentTypes, type]);
  }

  return (
    <div className="space-y-4">
      <TopicCard title="Employment type">
        <div className="flex flex-wrap gap-2">
          {EMPLOYMENT_TYPES.map(type => (
            <button
              key={type}
              type="button"
              onClick={() => toggleEmploymentType(type)}
              className={`rounded-lg px-3 py-1.5 text-sm font-medium transition-colors ${
                value.employmentTypes.includes(type)
                  ? "bg-blue-50 text-blue-700"
                  : "bg-gray-100 text-gray-500 hover:bg-gray-200"
              }`}
            >
              {type.replace("_", " ")}
            </button>
          ))}
        </div>
      </TopicCard>

      <TopicCard title="Location">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Field label="Countries you're eligible/willing to work in" value={value.countries} onChange={v => set("countries", v)} />
          <Field label="States/regions (optional)" value={value.states} onChange={v => set("states", v)} />
        </div>
        <div className="flex flex-wrap gap-4 text-sm text-gray-600">
          <label className="flex items-center gap-2">
            <input type="checkbox" checked={value.remoteAccepted} onChange={e => set("remoteAccepted", e.target.checked)} />
            Remote
          </label>
          <label className="flex items-center gap-2">
            <input type="checkbox" checked={value.hybridAccepted} onChange={e => set("hybridAccepted", e.target.checked)} />
            Hybrid
          </label>
          <label className="flex items-center gap-2">
            <input type="checkbox" checked={value.onsiteAccepted} onChange={e => set("onsiteAccepted", e.target.checked)} />
            On-site
          </label>
        </div>
      </TopicCard>

      <TopicCard title="Experience">
        <div>
          <label className={LABEL}>Seniority level</label>
          <select className={INPUT} value={value.seniorityLevel} onChange={e => set("seniorityLevel", e.target.value)}>
            {SENIORITY_LEVELS.map(l => <option key={l} value={l}>{l}</option>)}
          </select>
        </div>
      </TopicCard>

      <TopicCard title="Salary">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <Field label="Currency" value={value.currency} onChange={v => set("currency", v)} />
          <Field label="Minimum acceptable" value={value.salaryMin} onChange={v => set("salaryMin", v)} />
          <Field label="Target (upper end)" value={value.salaryMax} onChange={v => set("salaryMax", v)} />
        </div>
      </TopicCard>

      <TopicCard title="Skills">
        <Field label="Target skills/keywords (comma-separated)" value={value.skills} onChange={v => set("skills", v)} />
      </TopicCard>

      <TopicCard title="Disqualifiers">
        <Field label="Hard disqualifiers (one per line)" value={value.disqualifiers} onChange={v => set("disqualifiers", v)} multiline />
      </TopicCard>

      <TopicCard title="Company & role preferences">
        <Field label="Company preferences (one per line)" value={value.companyPreferences} onChange={v => set("companyPreferences", v)} multiline />
        <Field label="Role type preferences (one per line)" value={value.roleTypePreferences} onChange={v => set("roleTypePreferences", v)} multiline />
      </TopicCard>

      <AdvancedSection value={value.extra} onChange={v => set("extra", v)} />
    </div>
  );
}
