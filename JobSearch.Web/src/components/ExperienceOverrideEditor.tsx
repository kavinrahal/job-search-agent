import type { ExperienceEntry } from "../lib/backgroundYaml";
import type { ExperienceOverride } from "../types";
import { TopicCard, EntryCard, Field } from "./CardEditor";
import { BulletListEditor } from "./BulletListEditor";
import { getExperienceOverride, setExperienceOverride } from "../lib/resumeOverrides";

// Per-Background-experience-entry curation: whole-entry include/exclude, a reworded company
// description, and per-achievement reword/include/reorder + extra achievements with no
// Background source (see ItemOverride/ExperienceOverride in types.ts). Role/company/dates are
// shown read-only, sourced from Background — editing those facts stays on the Profile page (see
// the resume-builder plan's scope note); this editor only ever writes ExperienceOverride rows.
export function ExperienceOverrideEditor({ background, value, onChange }: {
  background: ExperienceEntry[];
  value: ExperienceOverride[];
  onChange: (v: ExperienceOverride[]) => void;
}) {
  if (background.length === 0) return null;

  return (
    <TopicCard title="Experience" count={`${background.length} role${background.length === 1 ? "" : "s"}`}>
      <p className="text-note text-faint">
        Fine-tune how each role from your background is shown on this resume — what's included,
        the wording, and the order of bullets. To edit the role, company, or dates themselves,
        use the Profile page.
      </p>
      <div className="space-y-3">
        {background.map((entry, index) => {
          const over = getExperienceOverride(value, index);
          const set = (patch: Partial<Omit<ExperienceOverride, "experienceIndex">>) =>
            onChange(setExperienceOverride(value, index, patch));
          const label = [entry.role, entry.company].filter(Boolean).join(" at ") || "Untitled role";

          return (
            <EntryCard key={index} summary={over.included ? label : `${label} (excluded)`}>
              <p className="text-note text-faint">
                {[entry.location, [entry.dates.start, entry.dates.end || "Present"].filter(Boolean).join(" – ")].filter(Boolean).join(" | ")}
              </p>
              <label className="flex items-center gap-2 text-body text-ink-2">
                <input type="checkbox" className="accent-ember" checked={over.included} onChange={e => set({ included: e.target.checked })} />
                Include this role
              </label>
              <Field
                label="Company description"
                value={over.companyDescriptionOverride ?? ""}
                onChange={v => set({ companyDescriptionOverride: v || null })}
                multiline
              />
              <BulletListEditor
                itemLabel="achievement"
                baseItems={entry.achievements}
                itemOverrides={over.achievements}
                extras={over.extraAchievements}
                onChangeItemOverrides={achievements => set({ achievements })}
                onChangeExtras={extraAchievements => set({ extraAchievements })}
              />
            </EntryCard>
          );
        })}
      </div>
    </TopicCard>
  );
}
