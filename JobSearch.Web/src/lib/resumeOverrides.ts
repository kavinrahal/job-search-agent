import type { ItemOverride, ExperienceOverride, ProjectOverride } from "../types";

// Pure, framework-free logic behind the resume builder's per-experience/per-project override
// editors — kept separate from the components so the reorder semantics (the trickiest, most
// important-to-get-right part of this feature) are pinned by plain unit tests instead of being
// buried in JSX, same split as resumeSections.ts/criteriaWizardMapping.ts.

export function getExperienceOverride(overrides: ExperienceOverride[], experienceIndex: number): ExperienceOverride {
  return overrides.find(o => o.experienceIndex === experienceIndex) ?? {
    experienceIndex, included: true, companyDescriptionOverride: null, achievements: [], extraAchievements: [], notes: null,
  };
}

export function setExperienceOverride(
  overrides: ExperienceOverride[], experienceIndex: number, patch: Partial<Omit<ExperienceOverride, "experienceIndex">>,
): ExperienceOverride[] {
  const next = { ...getExperienceOverride(overrides, experienceIndex), ...patch };
  return [...overrides.filter(o => o.experienceIndex !== experienceIndex), next];
}

export function getProjectOverride(overrides: ProjectOverride[], projectIndex: number): ProjectOverride {
  return overrides.find(o => o.projectIndex === projectIndex) ?? {
    projectIndex, included: true, descriptionOverride: null, highlights: [], extraHighlights: [],
  };
}

export function setProjectOverride(
  overrides: ProjectOverride[], projectIndex: number, patch: Partial<Omit<ProjectOverride, "projectIndex">>,
): ProjectOverride[] {
  const next = { ...getProjectOverride(overrides, projectIndex), ...patch };
  return [...overrides.filter(o => o.projectIndex !== projectIndex), next];
}

export interface BulletRow {
  index: number;
  text: string;
  included: boolean;
}

// Same two-group sort as ResumeRenderer.RenderBulletList (JobSearch.Data/ResumeRenderer.cs):
// items with an explicit Order always sort as group 0 (by that Order), everything else as
// group 1 (by natural Background index) — an explicit Order unconditionally wins, with natural
// order as the stable tie-break within each group. Mirroring this exactly is what makes the
// editor's visual bullet order match what Save will actually render.
export function orderedBulletRows(baseItems: string[], overrides: ItemOverride[]): BulletRow[] {
  const rows = baseItems.map((text, index) => {
    const over = overrides.find(o => o.index === index);
    return {
      index,
      text: over?.textOverride ?? text,
      included: over?.included ?? true,
      order: over?.order ?? null,
    };
  });
  return rows
    .map((row, naturalIndex) => ({ row, group: row.order !== null ? 0 : 1, key: row.order ?? naturalIndex }))
    .sort((a, b) => a.group - b.group || a.key - b.key)
    .map(x => x.row);
}

// Upserts the ItemOverride for `index`, preserving whatever wasn't in `patch`.
export function upsertItemOverride(
  overrides: ItemOverride[], index: number, patch: Partial<Omit<ItemOverride, "index">>,
): ItemOverride[] {
  const existing = overrides.find(o => o.index === index);
  const next: ItemOverride = {
    index,
    included: existing?.included ?? true,
    textOverride: existing?.textOverride ?? null,
    order: existing?.order ?? null,
    ...patch,
  };
  return [...overrides.filter(o => o.index !== index), next];
}

// Moves the bullet at `index` up/down in its current *visual* order (orderedBulletRows above),
// then writes an explicit Order for every non-extra bullet reflecting the new visual position —
// not just the two swapped items. No-op past either end, same "caller disables the button at
// the boundary" contract as resumeSections.ts's moveSection.
//
// Why every bullet, not just the two that moved: RenderBulletList's two-group sort always puts
// an explicit Order ahead of a natural (untouched) index. If a reorder only rewrote the two
// swapped items, a still-untouched bullet's natural index could numerically collide with one of
// the newly-explicit Order values (e.g. the first untouched bullet is naturally at 0, and the
// moved bullet is also given Order 0) — the render would then silently ignore the move. Writing
// an explicit Order for the whole list up front removes that collision risk entirely.
export function moveBulletOverride(
  baseItems: string[], overrides: ItemOverride[], index: number, direction: -1 | 1,
): ItemOverride[] {
  const visual = orderedBulletRows(baseItems, overrides);
  const from = visual.findIndex(r => r.index === index);
  const to = from + direction;
  if (from === -1 || to < 0 || to >= visual.length) return overrides;

  const reordered = [...visual];
  // from and to are both bounds-checked against visual.length above — plain array swap, same
  // pattern (and same justification) as resumeSections.ts's moveSection.
  // eslint-disable-next-line security/detect-object-injection
  [reordered[from], reordered[to]] = [reordered[to], reordered[from]];

  return reordered.map((row, position) => ({
    index: row.index,
    included: row.included,
    textOverride: overrides.find(o => o.index === row.index)?.textOverride ?? null,
    order: position,
  }));
}
