import { LABEL, INPUT, TopicCard, AddButton } from "./CardEditor";
import { IconButton, CloseIcon } from "../ui";
import type { SkillsSectionEntry } from "../types";

// Authors the resume's actual rendered Skills section directly — SkillsSectionEntry[] is
// independently authored per-resume, not derived from Background.skills at render time (see
// UserResume.cs's own comment on why: the same nested Background shape gets merged into one
// line for some categories and split into several for others, with no deterministic rule
// connecting the two). Pre-seeded from the resume's current skillsSection on load.
//
// Items uses onBlur/defaultValue rather than a fully controlled input, same as
// BackgroundEditor.tsx's own Skills category items — parsing the comma-separated text on every
// keystroke would fight the user mid-edit (a trailing ", " collapses away before they've typed
// the next item).
export function SkillsSectionEditor({ value, onChange }: {
  value: SkillsSectionEntry[];
  onChange: (v: SkillsSectionEntry[]) => void;
}) {
  function update(i: number, patch: Partial<SkillsSectionEntry>) {
    onChange(value.map((s, idx) => (idx === i ? { ...s, ...patch } : s)));
  }
  function remove(i: number) {
    onChange(value.filter((_, idx) => idx !== i));
  }
  function add() {
    onChange([...value, { label: "", items: [] }]);
  }
  function setItems(i: number, text: string) {
    update(i, { items: text.split(",").map(s => s.trim()).filter(Boolean) });
  }

  return (
    <TopicCard title="Skills" count={`${value.length} group${value.length === 1 ? "" : "s"}`}>
      <p className="text-note text-faint">
        The Skills section as it actually renders on your resume — one row per group, in order.
      </p>
      <div className="space-y-2">
        {value.map((entry, i) => (
          <div key={i} className="flex items-end gap-2">
            <div className="w-40 shrink-0">
              <label className={LABEL}>Label</label>
              <input className={INPUT} value={entry.label} onChange={e => update(i, { label: e.target.value })} />
            </div>
            <div className="flex-1">
              <label className={LABEL}>Items</label>
              <input
                className={INPUT}
                placeholder="Comma-separated"
                defaultValue={entry.items.join(", ")}
                onBlur={e => setItems(i, e.target.value)}
              />
            </div>
            <IconButton aria-label="Remove skills group" size="sm" className="mb-0.5" onClick={() => remove(i)}>
              <CloseIcon className="h-3.5 w-3.5" />
            </IconButton>
          </div>
        ))}
        <AddButton onClick={add}>+ Add skills group</AddButton>
      </div>
    </TopicCard>
  );
}
