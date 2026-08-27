import { useState } from "react";
import type { ResumeData, ResumeIndustry, SectionConfigEntry } from "../types";
import type { BackgroundData } from "../lib/backgroundYaml";
import { LABEL, Field, TopicCard } from "./CardEditor";
import { Button, ChipGroup, IconButton, ChevronDownIcon } from "../ui";
import { ExperienceOverrideEditor } from "./ExperienceOverrideEditor";
import { ProjectOverrideEditor } from "./ProjectOverrideEditor";
import { SkillsSectionEditor } from "./SkillsSectionEditor";
import { moveSection, toggleSectionIncluded, sectionLabel } from "../lib/resumeSections";

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
      <p className="text-note text-faint">
        Applying a template rewrites the section order and which sections are included below,
        based on what's typical for that industry. It never changes your summary or any
        experience/project wording.
      </p>
      <ChipGroup
        label="Industry"
        options={industries.map(i => ({ value: i.key, label: i.displayName }))}
        value={industryKey}
        onChange={setIndustryKey}
      />
      {industry?.hasSeniorityToggle && (
        <div>
          <label className={LABEL}>Seniority</label>
          <ChipGroup label="Seniority" options={SENIORITY_OPTIONS} value={seniority} onChange={setSeniority} />
        </div>
      )}
      <div>
        <Button
          size="sm"
          disabled={!industryKey || applying}
          onClick={() => industryKey && onApply(industryKey, industry?.hasSeniorityToggle ? seniority : undefined)}
        >
          {applying ? "Applying…" : "Apply template"}
        </Button>
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
      <p className="text-note text-faint">
        What's included in your resume, and in what order. Excluded sections stay in your
        background data, they just aren't rendered.
      </p>
      <div className="space-y-1">
        {value.map((section, i) => (
          <div key={section.sectionKey} className="surface-sunk flex items-center gap-3 rounded-ctl px-3 py-2">
            <input
              type="checkbox"
              className="accent-ember"
              checked={section.included}
              onChange={() => onChange(toggleSectionIncluded(value, i))}
              aria-label={`Include ${sectionLabel(section.sectionKey)}`}
            />
            <span className={`flex-1 text-body ${section.included ? "text-ink-2" : "text-faint line-through"}`}>
              {sectionLabel(section.sectionKey)}
            </span>
            <IconButton
              size="sm"
              disabled={i === 0}
              onClick={() => onChange(moveSection(value, i, -1))}
              aria-label={`Move ${sectionLabel(section.sectionKey)} up`}
            >
              <ChevronDownIcon className="h-3 w-3 rotate-180" />
            </IconButton>
            <IconButton
              size="sm"
              disabled={i === value.length - 1}
              onClick={() => onChange(moveSection(value, i, 1))}
              aria-label={`Move ${sectionLabel(section.sectionKey)} down`}
            >
              <ChevronDownIcon className="h-3 w-3" />
            </IconButton>
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
export function ResumeBuilder({
  value, onChange, industries, onApplyTemplate, applyingTemplate, background,
  onGenerateSummary, generatingSummary, generateSummaryError,
}: {
  value: ResumeData;
  onChange: (v: ResumeData) => void;
  industries: ResumeIndustry[];
  onApplyTemplate: (industryKey: string, seniority?: Seniority) => void;
  applyingTemplate: boolean;
  background: BackgroundData | null;
  onGenerateSummary: () => void;
  generatingSummary: boolean;
  generateSummaryError: string | null;
}) {
  return (
    <div className="space-y-4">
      <IndustryPicker industries={industries} onApply={onApplyTemplate} applying={applyingTemplate} />
      <SectionList value={value.sectionConfig} onChange={sectionConfig => onChange({ ...value, sectionConfig })} />
      <TopicCard title="Summary">
        <Field label="Resume summary" value={value.summary} onChange={summary => onChange({ ...value, summary })} multiline />
        <div>
          <Button size="sm" disabled={generatingSummary} onClick={onGenerateSummary}>
            {generatingSummary ? "Generating…" : "Generate summary"}
          </Button>
          {/* Rendered right by the button that triggers it, not the page's shared bottom error
              slot (used by Save/Apply template) — this page has enough content above the fold
              that a bottom-slot error is easy to miss without scrolling. Same inline-error
              styling as AdvancedSection's YAML error in CardEditor.tsx. */}
          {generateSummaryError && (
            <p className="mt-2 text-caption text-ember">{generateSummaryError}</p>
          )}
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
