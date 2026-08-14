import { useState } from "react";
import { useUpdateProfile } from "../hooks/useProfile";

const EMPLOYMENT_TYPES = ["full_time", "part_time", "contract", "casual"] as const;
const SENIORITY_LEVELS = ["junior", "mid", "senior", "lead"] as const;

function split(text: string, sep: string): string[] {
  return text.split(sep).map(s => s.trim()).filter(Boolean);
}

function yamlList(items: string[]): string {
  return items.length === 0 ? "[]" : `\n${items.map(i => `  - ${JSON.stringify(i)}`).join("\n")}`;
}

interface FormState {
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
}

const INITIAL_STATE: FormState = {
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

function buildCriteriaYaml(f: FormState): string {
  const lines = [
    `employment_type_preference: [${f.employmentTypes.join(", ")}]`,
    "",
    "location:",
    `  countries: ${yamlList(split(f.countries, ","))}`,
    `  states: ${yamlList(split(f.states, ","))}`,
    `  remote_accepted: ${f.remoteAccepted}`,
    `  hybrid_accepted: ${f.hybridAccepted}`,
    `  onsite_accepted: ${f.onsiteAccepted}`,
    "",
    "experience:",
    `  seniority_level: ${f.seniorityLevel}`,
    "",
    "salary:",
    `  currency: ${f.currency}`,
  ];
  if (f.salaryMin.trim()) lines.push(`  minimum_acceptable: ${f.salaryMin.trim()}`);
  if (f.salaryMax.trim()) lines.push(`  target_max: ${f.salaryMax.trim()}`);
  lines.push(
    "",
    "skill_dimensions:",
    `  - name: "Primary skills"`,
    `    keywords: ${yamlList(split(f.skills, ","))}`,
    "",
    `hard_disqualifiers: ${yamlList(split(f.disqualifiers, "\n"))}`,
    "",
    `company_preferences: ${yamlList(split(f.companyPreferences, "\n"))}`,
    "",
    `role_type_preferences: ${yamlList(split(f.roleTypePreferences, "\n"))}`,
  );
  return lines.join("\n");
}

const LABEL = "mb-1 block text-sm font-medium text-gray-700";
const INPUT = "w-full rounded-lg border border-gray-200 p-2 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-blue-300";

export function JobCriteriaPage() {
  const [form, setForm] = useState<FormState>(INITIAL_STATE);
  const [saved, setSaved] = useState(false);
  const { execute, loading: saving, error } = useUpdateProfile();

  function set<K extends keyof FormState>(key: K, value: FormState[K]) {
    setForm(f => ({ ...f, [key]: value }));
    setSaved(false);
  }

  function toggleEmploymentType(type: string) {
    set(
      "employmentTypes",
      form.employmentTypes.includes(type)
        ? form.employmentTypes.filter(t => t !== type)
        : [...form.employmentTypes, type],
    );
  }

  async function handleSave() {
    await execute({ jobCriteria: buildCriteriaYaml(form) });
    setSaved(true);
  }

  return (
    <div className="space-y-6">
      <h2 className="text-lg font-semibold text-gray-700">Job criteria</h2>

      <div className="space-y-5 rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
        <div>
          <label className={LABEL}>Employment type</label>
          <div className="flex flex-wrap gap-2">
            {EMPLOYMENT_TYPES.map(type => (
              <button
                key={type}
                type="button"
                onClick={() => toggleEmploymentType(type)}
                className={`rounded-lg px-3 py-1.5 text-sm font-medium transition-colors ${
                  form.employmentTypes.includes(type)
                    ? "bg-blue-50 text-blue-700"
                    : "bg-gray-100 text-gray-500 hover:bg-gray-200"
                }`}
              >
                {type.replace("_", " ")}
              </button>
            ))}
          </div>
        </div>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div>
            <label className={LABEL}>Countries you're eligible/willing to work in</label>
            <input
              className={INPUT}
              placeholder="Australia, New Zealand"
              value={form.countries}
              onChange={e => set("countries", e.target.value)}
            />
          </div>
          <div>
            <label className={LABEL}>States/regions (optional)</label>
            <input
              className={INPUT}
              placeholder="Victoria, NSW"
              value={form.states}
              onChange={e => set("states", e.target.value)}
            />
          </div>
        </div>

        <div>
          <label className={LABEL}>Work arrangement</label>
          <div className="flex flex-wrap gap-4 text-sm text-gray-600">
            <label className="flex items-center gap-2">
              <input type="checkbox" checked={form.remoteAccepted} onChange={e => set("remoteAccepted", e.target.checked)} />
              Remote
            </label>
            <label className="flex items-center gap-2">
              <input type="checkbox" checked={form.hybridAccepted} onChange={e => set("hybridAccepted", e.target.checked)} />
              Hybrid
            </label>
            <label className="flex items-center gap-2">
              <input type="checkbox" checked={form.onsiteAccepted} onChange={e => set("onsiteAccepted", e.target.checked)} />
              On-site
            </label>
          </div>
        </div>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div>
            <label className={LABEL}>Seniority level</label>
            <select className={INPUT} value={form.seniorityLevel} onChange={e => set("seniorityLevel", e.target.value)}>
              {SENIORITY_LEVELS.map(l => (
                <option key={l} value={l}>{l}</option>
              ))}
            </select>
          </div>
          <div>
            <label className={LABEL}>Currency</label>
            <input className={INPUT} value={form.currency} onChange={e => set("currency", e.target.value)} />
          </div>
        </div>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div>
            <label className={LABEL}>Minimum acceptable salary</label>
            <input
              type="number"
              className={INPUT}
              placeholder="100000"
              value={form.salaryMin}
              onChange={e => set("salaryMin", e.target.value)}
            />
          </div>
          <div>
            <label className={LABEL}>Target salary (upper end)</label>
            <input
              type="number"
              className={INPUT}
              placeholder="140000"
              value={form.salaryMax}
              onChange={e => set("salaryMax", e.target.value)}
            />
          </div>
        </div>

        <div>
          <label className={LABEL}>Target skills/keywords (comma-separated)</label>
          <input
            className={INPUT}
            placeholder="C#, ASP.NET Core, Azure, React"
            value={form.skills}
            onChange={e => set("skills", e.target.value)}
          />
        </div>

        <div>
          <label className={LABEL}>Hard disqualifiers (one per line)</label>
          <textarea
            className={INPUT}
            rows={3}
            placeholder="No visa sponsorship&#10;Requires relocation outside Australia"
            value={form.disqualifiers}
            onChange={e => set("disqualifiers", e.target.value)}
          />
        </div>

        <div>
          <label className={LABEL}>Company preferences (one per line)</label>
          <textarea
            className={INPUT}
            rows={3}
            placeholder="Product company with clear market fit&#10;Mid-sized, 50-500 employees"
            value={form.companyPreferences}
            onChange={e => set("companyPreferences", e.target.value)}
          />
        </div>

        <div>
          <label className={LABEL}>Role type preferences (one per line)</label>
          <textarea
            className={INPUT}
            rows={3}
            placeholder="Full-stack with backend ownership&#10;Product engineering, not pure maintenance"
            value={form.roleTypePreferences}
            onChange={e => set("roleTypePreferences", e.target.value)}
          />
        </div>

        <div className="flex items-center gap-3">
          <button
            onClick={handleSave}
            disabled={saving}
            className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:bg-gray-300"
          >
            {saving ? "Saving…" : "Save criteria"}
          </button>
          {saved && <span className="text-sm text-emerald-600">Saved.</span>}
        </div>
      </div>

      {error && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">{error}</div>
      )}
    </div>
  );
}
