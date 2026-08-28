import { useState, type ReactNode } from "react";
import { load as loadYaml, dump as dumpYaml } from "js-yaml";
import { Surface, Input, Textarea, Tooltip, ChevronDownIcon, CloseIcon } from "../ui";
import { cx } from "../ui/cx";

// Shared building blocks for the topic-card editing pattern used by BackgroundEditor and
// JobCriteriaEditor — a page-level topic (Personal, Experience, Location, Salary, ...) each
// gets its own card, with list-based topics collapsing individual entries by default so a
// long list doesn't dominate the page.
//
// LABEL/INPUT stay as exported class-string constants (rather than disappearing entirely)
// because several call sites compose a `<select>`/multi-select/checkbox row that doesn't fit
// Field's single-control shape — now built from the same tokens ui/Field.tsx uses internally,
// so they carry no manual dark: variant either.
export const LABEL = "mb-[5px] block text-meta font-[650] text-muted";
export const INPUT =
  "w-full rounded-ctl bg-sunk px-3 py-[9px] text-body text-ink border-0 placeholder:text-faint hairline-ring focus-ring " +
  "transition-[box-shadow,background-color] duration-400 ease-spring motion-reduce:transition-none";

export function Field({ label, value, onChange, multiline, type = "text", min, max, tooltip }: {
  label: string; value: string; onChange: (v: string) => void; multiline?: boolean;
  type?: "text" | "email" | "tel" | "number" | "month"; min?: number; max?: number; tooltip?: string;
}) {
  // ui/Field's label slot is plain text with no room for a trailing Tooltip trigger, and this is
  // the only Field call in the app that needs one (JobCriteriaEditor's skill-priority tooltip) —
  // not worth threading a tooltip prop through ui/Field for one caller, so this one case composes
  // the label row by hand instead, reusing the exact LABEL/INPUT tokens above.
  if (tooltip) {
    return (
      <div>
        <label className={LABEL}>
          {label}
          <Tooltip text={tooltip} />
        </label>
        {multiline ? (
          <textarea className={INPUT} rows={3} value={value} onChange={e => onChange(e.target.value)} />
        ) : (
          <input className={INPUT} type={type} min={min} max={max} value={value} onChange={e => onChange(e.target.value)} />
        )}
      </div>
    );
  }
  return multiline ? (
    <Textarea label={label} value={value} onChange={e => onChange(e.target.value)} />
  ) : (
    <Input label={label} type={type} min={min} max={max} value={value} onChange={e => onChange(e.target.value)} />
  );
}

// The outer "topic" grouping — stays expanded by default, since this is the structure
// itself, not dense content within it. No ready-made accordion exists in the design system, so
// this stays a bespoke composite, but built from Surface and the shared tokens rather than raw
// gray/dark: strings. `count` is an optional short summary (e.g. "4 roles") shown next to the
// collapse chevron — matches the prototype's collapsed-row counts for Experience/Skills/Projects
// on the resume builder (see #s5 in worksanta-slate.html); omit it for topics where a count
// doesn't make sense (Personal, Location, ...).
export function TopicCard({ title, children, defaultOpen = true, count }: { title: string; children: ReactNode; defaultOpen?: boolean; count?: string }) {
  const [open, setOpen] = useState(defaultOpen);
  return (
    <Surface padding="none" clip>
      <button
        type="button"
        onClick={() => setOpen(o => !o)}
        aria-expanded={open}
        className="flex w-full items-center justify-between p-4 text-left focus-ring"
      >
        <span className="text-lede font-[650] text-ink-2">{title}</span>
        <span className="flex items-center gap-2">
          {count && <span className="text-meta text-faint">{count}</span>}
          <ChevronDownIcon className={cx("h-3.5 w-3.5 flex-none text-faint transition-transform duration-300", open && "rotate-180")} />
        </span>
      </button>
      {open && <div className="animate-fade-in-up hairline-t space-y-4 p-4">{children}</div>}
    </Surface>
  );
}

// One item within a list-based topic — collapsed by default so a long list doesn't dominate
// the page; expand to read/edit it in full. onRemove is optional: some callers (e.g. the resume
// builder's per-experience/per-project override editors) show one card per *Background* entry,
// which can be included/excluded but never deleted from here — Background itself is edited on
// the Profile page. The Remove button only appears when a caller actually provides one.
export function EntryCard({ summary, defaultOpen = false, onRemove, children }: {
  summary: string; defaultOpen?: boolean; onRemove?: () => void; children: ReactNode;
}) {
  const [open, setOpen] = useState(defaultOpen);
  return (
    <div className="surface-sunk overflow-hidden rounded-ctl">
      <div className="flex items-center justify-between p-3">
        <button
          type="button"
          onClick={() => setOpen(o => !o)}
          aria-expanded={open}
          className="flex flex-1 items-center gap-1.5 text-left text-body font-[650] text-ink-2 focus-ring"
        >
          {summary || "New entry"}
          <ChevronDownIcon className={cx("h-3 w-3 flex-none text-faint transition-transform duration-300", open && "rotate-180")} />
        </button>
        {onRemove && (
          <button
            type="button"
            onClick={onRemove}
            aria-label="Remove"
            className="ml-2 inline-grid h-7 w-7 flex-none place-items-center rounded-ctl text-faint transition-[background-color,color,transform] duration-300 hover:bg-ember-wash hover:text-ember focus-ring tappable active:scale-[.94]"
          >
            <CloseIcon className="h-3.5 w-3.5" />
          </button>
        )}
      </div>
      {open && <div className="animate-fade-in-up hairline-t space-y-3 p-3">{children}</div>}
    </div>
  );
}

export function AddButton({ onClick, children }: { onClick: () => void; children: ReactNode }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="text-note font-[650] text-ember transition-colors hover:text-ember-hi focus-ring rounded-ctl"
    >
      {children}
    </button>
  );
}

// Shared by BackgroundEditor and JobCriteriaEditor for the sections their structured cards
// don't understand (e.g. a hand-authored "narrative" block, or per-item signals/tiers) —
// not worth bespoke UI for content this free-form, but still fully editable and never
// silently dropped.
export function AdvancedSection({ value, onChange }: { value: Record<string, unknown>; onChange: (v: Record<string, unknown>) => void }) {
  const [text, setText] = useState(() => dumpYaml(value, { lineWidth: -1 }));
  const [error, setError] = useState<string | null>(null);

  if (Object.keys(value).length === 0) return null;

  function handleBlur() {
    try {
      onChange((loadYaml(text) ?? {}) as Record<string, unknown>);
      setError(null);
    } catch {
      setError("Invalid YAML. Changes not applied, fix the syntax and click away again.");
    }
  }

  return (
    <TopicCard title="Advanced (raw YAML)" defaultOpen={false}>
      <p className="text-note text-faint">
        Sections here aren't yet supported by the structured editor above. Edit as YAML directly.
      </p>
      <textarea
        className={`${INPUT} font-mono`}
        rows={12}
        value={text}
        onChange={e => setText(e.target.value)}
        onBlur={handleBlur}
      />
      {error && <p className="text-caption text-ember">{error}</p>}
    </TopicCard>
  );
}
