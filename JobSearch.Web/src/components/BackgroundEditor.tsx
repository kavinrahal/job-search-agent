import type { BackgroundData, BackgroundParseResult, PersonalInfo, ExperienceEntry, EducationEntry, ProjectEntry } from "../lib/backgroundYaml";
import { LABEL, INPUT, Field, TopicCard, EntryCard, AddButton, AdvancedSection } from "./CardEditor";

const EMPLOYMENT_TYPES = ["full_time", "part_time", "contract", "casual", "internship"];

function PersonalSection({ value, onChange }: { value: PersonalInfo; onChange: (v: PersonalInfo) => void }) {
  const set = (key: keyof PersonalInfo, v: string) => onChange({ ...value, [key]: v });
  return (
    <TopicCard title="Personal">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <Field label="Name" value={value.name} onChange={v => set("name", v)} />
        <Field label="Email" value={value.email} onChange={v => set("email", v)} />
        <Field label="Phone" value={value.phone ?? ""} onChange={v => set("phone", v)} />
        <Field label="Location" value={value.location ?? ""} onChange={v => set("location", v)} />
        <Field label="LinkedIn" value={value.linkedin ?? ""} onChange={v => set("linkedin", v)} />
        <Field label="GitHub" value={value.github ?? ""} onChange={v => set("github", v)} />
      </div>
    </TopicCard>
  );
}

function ExperienceSection({ value, onChange }: { value: ExperienceEntry[]; onChange: (v: ExperienceEntry[]) => void }) {
  const update = (i: number, patch: Partial<ExperienceEntry>) =>
    onChange(value.map((e, idx) => (idx === i ? { ...e, ...patch } : e)));
  const remove = (i: number) => onChange(value.filter((_, idx) => idx !== i));
  const add = () => onChange([...value, {
    company: "", role: "", dates: { start: "", end: "" }, location: "", employment_type: "full_time", achievements: [],
  }]);

  return (
    <TopicCard title="Experience">
      <div className="space-y-3">
        {value.map((entry, i) => (
          <EntryCard key={i} summary={[entry.role, entry.company].filter(Boolean).join(" — ")} onRemove={() => remove(i)}>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <Field label="Company" value={entry.company} onChange={v => update(i, { company: v })} />
              <Field label="Role" value={entry.role} onChange={v => update(i, { role: v })} />
              <Field label="Start (YYYY-MM)" value={entry.dates.start} onChange={v => update(i, { dates: { ...entry.dates, start: v } })} />
              <Field label="End (YYYY-MM or present)" value={entry.dates.end} onChange={v => update(i, { dates: { ...entry.dates, end: v } })} />
              <Field label="Location" value={entry.location} onChange={v => update(i, { location: v })} />
              <div>
                <label className={LABEL}>Employment type</label>
                <select className={INPUT} value={entry.employment_type} onChange={e => update(i, { employment_type: e.target.value })}>
                  {EMPLOYMENT_TYPES.map(t => <option key={t} value={t}>{t.replace("_", " ")}</option>)}
                </select>
              </div>
            </div>
            <div>
              <label className={LABEL}>Achievements</label>
              <div className="space-y-2">
                {entry.achievements.map((a, ai) => (
                  <div key={ai} className="flex gap-2">
                    <textarea
                      className={`${INPUT} flex-1`}
                      rows={2}
                      value={a}
                      onChange={e => update(i, {
                        achievements: entry.achievements.map((x, idx) => (idx === ai ? e.target.value : x)),
                      })}
                    />
                    <button
                      onClick={() => update(i, { achievements: entry.achievements.filter((_, idx) => idx !== ai) })}
                      className="text-xs text-red-500 hover:text-red-700"
                    >
                      &#10005;
                    </button>
                  </div>
                ))}
                <AddButton onClick={() => update(i, { achievements: [...entry.achievements, ""] })}>+ Add achievement</AddButton>
              </div>
            </div>
          </EntryCard>
        ))}
        <AddButton onClick={add}>+ Add role</AddButton>
      </div>
    </TopicCard>
  );
}

function EducationSection({ value, onChange }: { value: EducationEntry[]; onChange: (v: EducationEntry[]) => void }) {
  const update = (i: number, patch: Partial<EducationEntry>) =>
    onChange(value.map((e, idx) => (idx === i ? { ...e, ...patch } : e)));
  const remove = (i: number) => onChange(value.filter((_, idx) => idx !== i));
  const add = () => onChange([...value, { institution: "", degree: "", location: "", graduation_year: "" }]);

  return (
    <TopicCard title="Education">
      <div className="space-y-3">
        {value.map((entry, i) => (
          <EntryCard key={i} summary={[entry.degree, entry.institution].filter(Boolean).join(" — ")} onRemove={() => remove(i)}>
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <Field label="Institution" value={entry.institution} onChange={v => update(i, { institution: v })} />
              <Field label="Degree" value={entry.degree} onChange={v => update(i, { degree: v })} />
              <Field label="Location" value={entry.location} onChange={v => update(i, { location: v })} />
              <Field label="Graduation year" value={String(entry.graduation_year)} onChange={v => update(i, { graduation_year: v })} />
            </div>
          </EntryCard>
        ))}
        <AddButton onClick={add}>+ Add education</AddButton>
      </div>
    </TopicCard>
  );
}

function ProjectsSection({ value, onChange }: { value: ProjectEntry[]; onChange: (v: ProjectEntry[]) => void }) {
  const update = (i: number, patch: Partial<ProjectEntry>) =>
    onChange(value.map((e, idx) => (idx === i ? { ...e, ...patch } : e)));
  const remove = (i: number) => onChange(value.filter((_, idx) => idx !== i));
  const add = () => onChange([...value, { name: "", description: "" }]);

  return (
    <TopicCard title="Projects">
      <div className="space-y-3">
        {value.map((entry, i) => (
          <EntryCard key={i} summary={entry.name} onRemove={() => remove(i)}>
            <Field label="Name" value={entry.name} onChange={v => update(i, { name: v })} />
            <Field label="Description" value={entry.description} onChange={v => update(i, { description: v })} multiline />
            <Field label="Tech stack" value={entry.tech_stack ?? ""} onChange={v => update(i, { tech_stack: v })} />
          </EntryCard>
        ))}
        <AddButton onClick={add}>+ Add project</AddButton>
      </div>
    </TopicCard>
  );
}

function SkillsSection({ value, onChange }: { value: Record<string, string[]>; onChange: (v: Record<string, string[]>) => void }) {
  const categories = Object.keys(value);

  function renameCategory(oldName: string, newName: string) {
    if (!newName.trim() || newName === oldName) return;
    const { [oldName]: items, ...rest } = value;
    onChange({ ...rest, [newName]: items });
  }
  function setItems(category: string, text: string) {
    onChange({ ...value, [category]: text.split(",").map(s => s.trim()).filter(Boolean) });
  }
  function removeCategory(category: string) {
    const { [category]: _removed, ...rest } = value;
    onChange(rest);
  }
  function addCategory() {
    let name = "new category", n = 1;
    while (name in value) name = `new category ${++n}`;
    onChange({ ...value, [name]: [] });
  }

  return (
    <TopicCard title="Skills">
      <div className="space-y-3">
        {categories.map(category => (
          <div key={category} className="flex gap-2">
            <input
              className={`${INPUT} w-40 shrink-0 font-medium`}
              defaultValue={category}
              onBlur={e => renameCategory(category, e.target.value)}
            />
            <input
              className={`${INPUT} flex-1`}
              placeholder="Comma-separated"
              defaultValue={value[category].join(", ")}
              onBlur={e => setItems(category, e.target.value)}
            />
            <button onClick={() => removeCategory(category)} className="text-xs text-red-500 hover:text-red-700">&#10005;</button>
          </div>
        ))}
        <AddButton onClick={addCategory}>+ Add category</AddButton>
      </div>
    </TopicCard>
  );
}

function StructuredBackgroundEditor({ value, onChange }: { value: BackgroundData; onChange: (v: BackgroundData) => void }) {
  return (
    <div className="space-y-4">
      <PersonalSection value={value.personal} onChange={v => onChange({ ...value, personal: v })} />
      <ExperienceSection value={value.experience} onChange={v => onChange({ ...value, experience: v })} />
      <EducationSection value={value.education} onChange={v => onChange({ ...value, education: v })} />
      <SkillsSection value={value.skills} onChange={v => onChange({ ...value, skills: v })} />
      <ProjectsSection value={value.projects} onChange={v => onChange({ ...value, projects: v })} />
      <AdvancedSection value={value.extra} onChange={v => onChange({ ...value, extra: v })} />
    </div>
  );
}

// Handles both branches of parseBackgroundYaml's result: structured cards when the stored
// YAML parses cleanly, or a raw-text fallback (nothing lost, just not broken into cards)
// when it doesn't — see backgroundYaml.ts for why that can happen on real, pre-existing data.
export function BackgroundEditor({ value, onChange }: { value: BackgroundParseResult; onChange: (v: BackgroundParseResult) => void }) {
  if (!value.ok) {
    return (
      <div className="space-y-2 rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
        <p className="text-xs text-amber-600">
          This background doesn't parse as structured YAML, so it's shown as raw text instead —
          nothing has been lost, edit it directly below.
        </p>
        <textarea
          className={`${INPUT} font-mono`}
          rows={16}
          value={value.rawText}
          onChange={e => onChange({ ok: false, rawText: e.target.value })}
        />
      </div>
    );
  }
  return <StructuredBackgroundEditor value={value.data} onChange={data => onChange({ ok: true, data })} />;
}
