import type { ReactNode } from "react";
import { cx } from "./cx";

// Pill selection. Supersedes components/ChoiceButtons.tsx, and deliberately keeps its option shape
// and its multi/single discriminated union so the later swap is an import change.
//
// Chips are always full pills. That is the shape lock, and it is also what separates a chip
// ("choose some of these") from a Badge ("this is its status"), which is a 5px mark.

export interface ChipOption<T extends string = string> {
  value: T;
  label: string;
}

export interface ChipProps {
  children: ReactNode;
  selected: boolean;
  onClick: () => void;
  /** "checkbox" for multi-select, "radio" for single. ChipGroup picks this for you. */
  role?: "checkbox" | "radio";
  disabled?: boolean;
  className?: string;
}

export function Chip({ children, selected, onClick, role = "checkbox", disabled, className }: ChipProps) {
  return (
    <button
      type="button"
      role={role}
      aria-checked={selected}
      disabled={disabled}
      onClick={onClick}
      className={cx(
        "rounded-pill px-[13px] py-1.5 text-note font-[650] whitespace-nowrap focus-ring tappable",
        "transition-[background-color,color,box-shadow,transform] duration-350 ease-spring motion-reduce:transition-none",
        "active:scale-[.97] disabled:pointer-events-none disabled:opacity-55",
        selected ? "bg-ember text-on-ember" : "bg-core text-ink-2 hairline-ring-2 hover:text-ink",
        className,
      )}
    >
      {children}
    </button>
  );
}

type ChipGroupProps<T extends string> = {
  /** Names the group for assistive tech. An unlabelled group announces as nothing. */
  label: string;
  options: ChipOption<T>[];
  className?: string;
} & (
  | { multi?: false; value: T | null; onChange: (value: T) => void }
  | { multi: true; value: T[]; onChange: (value: T[]) => void }
);

export function ChipGroup<T extends string>(props: ChipGroupProps<T>) {
  const { label, options, className } = props;
  const isSelected = (v: T) => (props.multi ? props.value.includes(v) : props.value === v);

  function handleClick(v: T) {
    if (props.multi) {
      props.onChange(props.value.includes(v) ? props.value.filter(x => x !== v) : [...props.value, v]);
    } else {
      props.onChange(v);
    }
  }

  return (
    // A multi-select is a group of independent checkboxes, so each is its own tab stop and there is
    // no arrow-key contract to honour. Single-select uses radiogroup for the same reason
    // SegmentedControl does.
    <div role={props.multi ? "group" : "radiogroup"} aria-label={label} className={cx("flex flex-wrap gap-1.5", className)}>
      {options.map(option => (
        <Chip
          key={option.value}
          role={props.multi ? "checkbox" : "radio"}
          selected={isSelected(option.value)}
          onClick={() => handleClick(option.value)}
        >
          {option.label}
        </Chip>
      ))}
    </div>
  );
}
