import { useState } from "react";
import type { ResumeData, ResumeIndustry, SectionConfigEntry } from "../types";
import type { BackgroundData } from "../lib/backgroundYaml";
import { LABEL, Field, TopicCard } from "./CardEditor";
import { ChoiceButtons } from "./ChoiceButtons";
import { ExperienceOverrideEditor } from "./ExperienceOverrideEditor";
import { ProjectOverrideEditor } from "./ProjectOverrideEditor";
import { SkillsSectionEditor } from "./SkillsSectionEditor";
import { moveSection, toggleSectionIncluded, sectionLabel } from "../lib/resumeSections";
import { PRIMARY_BUTTON_SM } from "../lib/styles";

type Seniority = "junior" | "experienced";
const SENIORITY_OPTIONS = [
  { value: "junior" as Seniority, label: "Junior / campus" },
  { value: "experienced" as Seniority, label: "Experienced" },
];

// Industry template picker: choose an industry (+ seniority, only for industries research found
// a junior/experienced split for — see ResumeIndustryTemplates.cs) and apply its default section
// order below. Overwrites only the section list — Summary and every other UserResume field are
// untouched by this action (see POST /resume/apply-template's own comment), so it's always safe
// to try, even after manual edits elsewhere.
function IndustryPicker({ industries, onApply, applying }: {
  industries: ResumeIndustry[];
  onApply: (industryKey: string, seniority?: Seniority) => void;
  applying: boolean;
}) {
  const [industryKey, setIndustryKey] = useState<string | null>(null);
  const [seniority, setSeniority] = useState<Seniority>("experienced");
  const industry = industries.find(i => i.key === industryKey) ?? null;

  return (
    <TopicCard title="Industry template">
      <p className="text-xs text-gray-400 dark:text-gray-500">
        Applying a template rewrites the section order and which sections are included below,
        based on what's typical for that industry. It never changes your summary or any
        experience/project wording.
      </p>
      <ChoiceButtons
        options={industries.map(i => ({ value: i.key, label: i.displayName }))}
        value={industryKey}
        onChange={setIndustryKey}
      />
      {industry?.hasSeniorityToggle && (
        <div>
          <label className={LABEL}>Seniority</label>
          <ChoiceButtons options={SENIORITY_OPTIONS} value={seniority} onChange={setSeniority} />
        </div>
      )}
      <div>
        <button
          type="button"
          disabled={!industryKey || applying}
          onClick={() => industryKey && onApply(industryKey, industry?.hasSeniorityToggle ? seniority : undefined)}
          className={PRIMARY_BUTTON_SM}
        >
          {applying ? "Applying…" : "Apply template"}
        </button>
      </div>
    </TopicCard>
  );
}

// One row per section: include/exclude and reorder (up/down — no drag library, see the plan's
// scope note). Reordering only ever swaps adjacent rows, so array-index swap is enough; nothing
// here needs a stable id beyond the row's current position, and sectionKey is unique within the
// list so it's a safe React key too.
function SectionList({ value, onChange }: { value: SectionConfigEntry[]; onChange: (v: SectionConfigEntry[]) => void }) {
  return (
    <TopicCard title="Sections">
      <p className="text-xs text-gray-400 dark:text-gray-500">
        What's included in your resume, and in what order. Excluded sections stay in your
        background data, they just aren't rendered.
      </p>
      <div className="space-y-1">
        {value.map((section, i) => (
          <div
            key={section.sectionKey}
            className="flex items-center gap-3 rounded-lg border border-gray-100 bg-gray-50 px-3 py-2 dark:border-gray-800 dark:bg-gray-800/50"
          >
            <input
              type="checkbox"
              checked={section.included}
              onChange={() => onChange(toggleSectionIncluded(value, i))}
              aria-label={`Include ${sectionLabel(section.sectionKey)}`}
            />
            <span
              className={`flex-1 text-sm ${
                section.included ? "text-gray-700 dark:text-gray-200" : "text-gray-400 line-through dark:text-gray-600"
              }`}
            >
              {sectionLabel(section.sectionKey)}
            </span>
            <button
              type="button"
              disabled={i === 0}
              onClick={() => onChange(moveSection(value, i, -1))}
              aria-label={`Move ${sectionLabel(section.sectionKey)} up`}
              className="text-gray-400 transition-colors hover:text-gray-700 disabled:opacity-30 dark:hover:text-gray-200"
            >
              &#9650;
            </button>
            <button
              type="button"
              disabled={i === value.length - 1}
              onClick={() => onChange(moveSection(value, i, 1))}
              aria-label={`Move ${sectionLabel(section.sectionKey)} down`}
              className="text-gray-400 transition-colors hover:text-gray-700 disabled:opacity-30 dark:hover:text-gray-200"
            >
              &#9660;
            </button>
          </div>
        ))}
      </div>
    </TopicCard>
  );
}

// The full user-facing surface for UserResume (the curation layer over Background — see
// UserResume.cs): industry template, section include/reorder, summary text (hand-written or
// auto-generated via the "Generate summary" button, grounded in `background` + target job
// titles — see ResumeSummaryAgent), and per-experience/project/skills curation. `background` is
// Background's read-only Experience/Projects entries (role/company/dates, achievements/
// highlights) that the override editors need to show what they're overriding — null while
// Background hasn't loaded yet or doesn't parse as structured YAML (see parseBackgroundYaml), in
// which case those editors just don't render; nothing else on this page depends on it.
export function ResumeBuilder({ value, onChange, industries, onApplyTemplate, applyingTemplate, background, onGenerateSummary, generatingSummary }: {
  value: ResumeData;
  onChange: (v: ResumeData) => void;
  industries: ResumeIndustry[];
  onApplyTemplate: (industryKey: string, seniority?: Seniority) => void;
  applyingTemplate: boolean;
  background: BackgroundData | null;
  onGenerateSummary: () => void;
  generatingSummary: boolean;
}) {
  return (
    <div className="space-y-4">
      <IndustryPicker industries={industries} onApply={onApplyTemplate} applying={applyingTemplate} />
      <SectionList value={value.sectionConfig} onChange={sectionConfig => onChange({ ...value, sectionConfig })} />
      <TopicCard title="Summary">
        <Field label="Resume summary" value={value.summary} onChange={summary => onChange({ ...value, summary })} multiline />
        <div>
          <button
            type="button"
            disabled={generatingSummary}
            onClick={onGenerateSummary}
            className={PRIMARY_BUTTON_SM}
          >
            {generatingSummary ? "Generating…" : "Generate summary"}
          </button>
        </div>
      </TopicCard>
      {background && (
        <>
          <ExperienceOverrideEditor
            background={background.experience}
            value={value.experienceOverrides}
            onChange={experienceOverrides => onChange({ ...value, experienceOverrides })}
          />
          <ProjectOverrideEditor
            background={background.projects}
            value={value.projectOverrides}
            onChange={projectOverrides => onChange({ ...value, projectOverrides })}
          />
        </>
      )}
      <SkillsSectionEditor value={value.skillsSection} onChange={skillsSection => onChange({ ...value, skillsSection })} />
    </div>
  );
}
