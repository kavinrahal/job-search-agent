import type { ReactNode } from "react";

// Layout helpers for the gallery. Nothing here is part of the design system — it is scaffolding
// for reviewing it, and it deliberately uses the same tokens so the scaffolding never competes
// visually with the specimens.

export function GallerySection({ id, title, note, children }: { id: string; title: string; note?: string; children: ReactNode }) {
  return (
    <section id={id} className="scroll-mt-20">
      <h2 className="hairline-b mt-12 mb-3.5 pb-2 text-eyebrow tracking-[.16em] text-faint uppercase">{title}</h2>
      {note && <p className="mt-0 mb-4 max-w-[74ch] text-caption text-pretty text-muted">{note}</p>}
      <div className="flex flex-col gap-5">{children}</div>
    </section>
  );
}

/** One component, one state. `label` is what a reviewer reads when something looks wrong. */
export function Specimen({ label, children, wide = false }: { label: string; children: ReactNode; wide?: boolean }) {
  return (
    <div>
      <p className="mb-1.5 text-meta text-faint">{label}</p>
      <div className={wide ? "" : "flex flex-wrap items-center gap-2.5"}>{children}</div>
    </div>
  );
}

/** A row of related specimens that should be compared against each other. */
export function SpecimenGrid({ children }: { children: ReactNode }) {
  return <div className="grid gap-3.5 sm:grid-cols-2">{children}</div>;
}
