import type { ProjectEntry } from "../lib/backgroundYaml";
import type { ProjectOverride } from "../types";
import { TopicCard, EntryCard, Field } from "./CardEditor";
import { BulletListEditor } from "./BulletListEditor";
import { getProjectOverride, setProjectOverride } from "../lib/resumeOverrides";

// Identical shape/pattern to ExperienceOverrideEditor, against ProjectOverride and
// Background.projects — see that component's own comment for the full reasoning (kept in one
// place rather than duplicated here).
export function ProjectOverrideEditor({ background, value, onChange }: {
  background: ProjectEntry[];
  value: ProjectOverride[];
  onChange: (v: ProjectOverride[]) => void;
}) {
  if (background.length === 0) return null;

  return (
    <TopicCard title="Projects" count={`${background.length} listed`}>
      <p className="text-note text-faint">
        Fine-tune how each project from your background is shown on this resume. To edit the
        project itself, use the Profile page.
      </p>
      <div className="space-y-3">
        {background.map((entry, index) => {
          const over = getProjectOverride(value, index);
          const set = (patch: Partial<Omit<ProjectOverride, "projectIndex">>) =>
            onChange(setProjectOverride(value, index, patch));
          const label = entry.name || "Untitled project";

          return (
            <EntryCard key={index} summary={over.included ? label : `${label} (excluded)`}>
              <label className="flex items-center gap-2 text-body text-ink-2">
                <input type="checkbox" className="accent-ember" checked={over.included} onChange={e => set({ included: e.target.checked })} />
                Include this project
              </label>
              <Field
                label="Description"
                value={over.descriptionOverride ?? ""}
                onChange={v => set({ descriptionOverride: v || null })}
                multiline
              />
              <BulletListEditor
                itemLabel="highlight"
                baseItems={entry.highlights ?? []}
                itemOverrides={over.highlights}
                extras={over.extraHighlights}
                onChangeItemOverrides={highlights => set({ highlights })}
                onChangeExtras={extraHighlights => set({ extraHighlights })}
              />
            </EntryCard>
          );
        })}
      </div>
    </TopicCard>
  );
}
