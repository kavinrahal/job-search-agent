import { useState, type ReactNode } from "react";
import { load as loadYaml, dump as dumpYaml } from "js-yaml";
import { InfoTooltip } from "./InfoTooltip";

// Shared building blocks for the topic-card editing pattern used by BackgroundEditor and
// JobCriteriaEditor — a page-level topic (Personal, Experience, Location, Salary, ...) each
// gets its own card, with list-based topics collapsing individual entries by default so a
// long list doesn't dominate the page.

export const LABEL = "mb-1 block text-xs font-medium text-gray-500 dark:text-gray-400";
export const INPUT = "w-full rounded-lg border border-gray-200 bg-white p-2 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-violet-400 dark:border-gray-700 dark:bg-gray-800 dark:text-gray-100 dark:focus:ring-violet-500";

export function Field({ label, value, onChange, multiline, type = "text", min, max, tooltip }: {
  label: string; value: string; onChange: (v: string) => void; multiline?: boolean;
  type?: "text" | "email" | "tel" | "number" | "month"; min?: number; max?: number; tooltip?: string;
}) {
  return (
    <div>
      <label className={LABEL}>
        {label}
        {tooltip && <InfoTooltip text={tooltip} />}
      </label>
      {multiline ? (
        <textarea className={INPUT} rows={3} value={value} onChange={e => onChange(e.target.value)} />
      ) : (
        <input className={INPUT} type={type} min={min} max={max} value={value} onChange={e => onChange(e.target.value)} />
      )}
    </div>
  );
}

// The outer "topic" grouping — stays expanded by default, since this is the structure
// itself, not dense content within it.
export function TopicCard({ title, children, defaultOpen = true }: { title: string; children: ReactNode; defaultOpen?: boolean }) {
  const [open, setOpen] = useState(defaultOpen);
  return (
    <div className="rounded-xl border border-gray-200 bg-white shadow-sm dark:border-gray-800 dark:bg-gray-900">
      <button
        onClick={() => setOpen(o => !o)}
        className="flex w-full items-center justify-between p-4 text-left"
      >
        <span className="text-sm font-semibold text-gray-700 dark:text-gray-200">{title}</span>
        <span className="text-gray-400 dark:text-gray-500">{open ? "−" : "+"}</span>
      </button>
      {open && <div className="animate-fade-in-up space-y-4 border-t border-gray-100 p-4 dark:border-gray-800">{children}</div>}
    </div>
  );
}

// One item within a list-based topic — collapsed by default so a long list doesn't dominate
// the page; expand to read/edit it in full.
export function EntryCard({ summary, defaultOpen = false, onRemove, children }: {
  summary: string; defaultOpen?: boolean; onRemove: () => void; children: ReactNode;
}) {
  const [open, setOpen] = useState(defaultOpen);
  return (
    <div className="rounded-lg border border-gray-100 bg-gray-50 dark:border-gray-800 dark:bg-gray-800/50">
      <div className="flex items-center justify-between p-3">
        <button onClick={() => setOpen(o => !o)} className="flex-1 text-left text-sm font-medium text-gray-700 dark:text-gray-200">
          {summary || "New entry"} {open ? "▲" : "▼"}
        </button>
        <button onClick={onRemove} className="ml-2 text-xs text-red-500 transition-colors hover:text-red-700 dark:text-red-400 dark:hover:text-red-300">Remove</button>
      </div>
      {open && <div className="animate-fade-in-up space-y-3 border-t border-gray-100 p-3 dark:border-gray-800">{children}</div>}
    </div>
  );
}

export function AddButton({ onClick, children }: { onClick: () => void; children: ReactNode }) {
  return (
    <button onClick={onClick} className="text-sm font-medium text-violet-600 transition-colors hover:text-violet-700 dark:text-violet-400 dark:hover:text-violet-300">
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
      <p className="text-xs text-gray-400 dark:text-gray-500">
        Sections here aren't yet supported by the structured editor above. Edit as YAML directly.
      </p>
      <textarea
        className={`${INPUT} font-mono`}
        rows={12}
        value={text}
        onChange={e => setText(e.target.value)}
        onBlur={handleBlur}
      />
      {error && <p className="text-xs text-red-600 dark:text-red-400">{error}</p>}
    </TopicCard>
  );
}
