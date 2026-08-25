import { LABEL, INPUT, AddButton } from "./CardEditor";
import type { ItemOverride } from "../types";
import { orderedBulletRows, upsertItemOverride, moveBulletOverride } from "../lib/resumeOverrides";

// Shared bullet-list editor behind ExperienceOverrideEditor (Background achievements) and
// ProjectOverrideEditor (Background highlights) — identical override shape (ItemOverride), so
// one component instead of two near-duplicates.
//
// Background-sourced bullets (baseItems) can be reworded, included/excluded, and reordered (no
// drag library — up/down buttons, same precedent as ResumeBuilder.tsx's SectionList) but never
// deleted, since they aren't ours to delete — deleting a Background achievement/highlight
// happens on the Profile page, not here. "Extra" bullets (no Background source at all) are
// fully ours, so those get a real add/remove list, following BackgroundEditor.tsx's
// achievements block for that part.
export function BulletListEditor({ itemLabel, baseItems, itemOverrides, extras, onChangeItemOverrides, onChangeExtras }: {
  itemLabel: string;
  baseItems: string[];
  itemOverrides: ItemOverride[];
  extras: string[];
  onChangeItemOverrides: (v: ItemOverride[]) => void;
  onChangeExtras: (v: string[]) => void;
}) {
  const rows = orderedBulletRows(baseItems, itemOverrides);
  const label = itemLabel.charAt(0).toUpperCase() + itemLabel.slice(1);

  return (
    <div className="space-y-2">
      <label className={LABEL}>{label}s</label>
      <div className="space-y-2">
        {rows.map((row, position) => (
          <div key={row.index} className="flex items-start gap-2">
            <input
              type="checkbox"
              className="mt-2.5 shrink-0"
              checked={row.included}
              aria-label={row.included ? `Exclude this ${itemLabel}` : `Include this ${itemLabel}`}
              onChange={e => onChangeItemOverrides(upsertItemOverride(itemOverrides, row.index, { included: e.target.checked }))}
            />
            <textarea
              className={`${INPUT} flex-1 ${row.included ? "" : "text-gray-400 line-through dark:text-gray-600"}`}
              rows={2}
              value={row.text}
              onChange={e => onChangeItemOverrides(upsertItemOverride(itemOverrides, row.index, { textOverride: e.target.value }))}
            />
            <div className="flex shrink-0 flex-col">
              <button
                type="button"
                disabled={position === 0}
                onClick={() => onChangeItemOverrides(moveBulletOverride(baseItems, itemOverrides, row.index, -1))}
                aria-label={`Move this ${itemLabel} up`}
                className="text-gray-400 transition-colors hover:text-gray-700 disabled:opacity-30 dark:hover:text-gray-200"
              >
                &#9650;
              </button>
              <button
                type="button"
                disabled={position === rows.length - 1}
                onClick={() => onChangeItemOverrides(moveBulletOverride(baseItems, itemOverrides, row.index, 1))}
                aria-label={`Move this ${itemLabel} down`}
                className="text-gray-400 transition-colors hover:text-gray-700 disabled:opacity-30 dark:hover:text-gray-200"
              >
                &#9660;
              </button>
            </div>
          </div>
        ))}
      </div>
      <div className="space-y-2">
        {extras.map((extra, i) => (
          <div key={i} className="flex gap-2">
            <textarea
              className={`${INPUT} flex-1`}
              rows={2}
              value={extra}
              onChange={e => onChangeExtras(extras.map((x, idx) => (idx === i ? e.target.value : x)))}
            />
            <button
              onClick={() => onChangeExtras(extras.filter((_, idx) => idx !== i))}
              className="text-xs text-red-500 transition-colors hover:text-red-700 dark:text-red-400 dark:hover:text-red-300"
            >
              &#10005;
            </button>
          </div>
        ))}
        <AddButton onClick={() => onChangeExtras([...extras, ""])}>+ Add extra {itemLabel}</AddButton>
      </div>
    </div>
  );
}
