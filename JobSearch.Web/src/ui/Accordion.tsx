import { useId, useState, type ReactNode } from "react";
import { cx } from "./cx";
import { ChevronDownIcon } from "./icons";

// A single-open FAQ list. Clicking an open item closes it; clicking a closed one opens it and
// closes whichever was open, since two answers open at once on a marketing page reads as clutter
// rather than as content.
//
// The panel stays in the DOM at all times and animates via a `grid-template-rows` 0fr/1fr swap
// rather than `height: auto` (which cannot transition) or mount/unmount (which cannot animate at
// all). It is the same "animate a wrapper, let the child size itself" trick, just on the grid axis
// instead of a transform. `aria-hidden` on the closed panel keeps screen readers from reading
// content that is visually collapsed to zero height.

export interface AccordionItemData {
  question: string;
  answer: ReactNode;
}

export interface AccordionProps {
  items: AccordionItemData[];
  className?: string;
}

export function Accordion({ items, className }: AccordionProps) {
  const [openIndex, setOpenIndex] = useState<number | null>(null);
  const baseId = useId();

  return (
    <div className={cx("surface-core overflow-hidden", className)}>
      {items.map((item, index) => {
        const open = openIndex === index;
        const triggerId = `${baseId}-trigger-${index}`;
        const panelId = `${baseId}-panel-${index}`;

        return (
          <div key={item.question} className={cx(index > 0 && "hairline-t")}>
            <h3 className="m-0">
              <button
                type="button"
                id={triggerId}
                aria-expanded={open}
                aria-controls={panelId}
                onClick={() => setOpenIndex(open ? null : index)}
                className="focus-ring tappable flex w-full items-center justify-between gap-3 px-3.5 py-3 text-left"
              >
                <span className="text-body font-[650] text-ink">{item.question}</span>
                <ChevronDownIcon
                  className={cx(
                    "h-4 w-4 flex-none text-faint transition-transform duration-350 ease-spring motion-reduce:transition-none",
                    open && "rotate-180",
                  )}
                />
              </button>
            </h3>
            <div
              id={panelId}
              role="region"
              aria-labelledby={triggerId}
              aria-hidden={!open}
              className={cx(
                "grid transition-[grid-template-rows] duration-350 ease-spring motion-reduce:transition-none",
                open ? "grid-rows-[1fr]" : "grid-rows-[0fr]",
              )}
            >
              <div className="overflow-hidden">
                <p className="m-0 px-3.5 pb-3.5 text-body text-muted">{item.answer}</p>
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
}
