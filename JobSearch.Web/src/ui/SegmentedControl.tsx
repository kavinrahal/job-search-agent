import { useRef, type KeyboardEvent } from "react";
import { cx } from "./cx";

// The tab/filter segment: "All 14 · Strong 2 · Good 5 · Weak 7".
//
// A radiogroup, not a tablist, because it filters a list that is already on screen rather than
// swapping between panels. That choice determines the keyboard contract, which is the ARIA
// radiogroup one: one tab stop for the whole control, arrow keys move *and* select, Home/End jump
// to the ends, and the arrows wrap.

export interface Segment<T extends string> {
  value: T;
  label: string;
  /** The trailing count in the design, e.g. "Strong" + 2. Rendered dimmer than the label. */
  count?: number;
}

export interface SegmentedControlProps<T extends string> {
  /** Names the group for assistive tech. Required: an unlabelled radiogroup announces as nothing. */
  label: string;
  segments: Segment<T>[];
  value: T;
  onChange: (value: T) => void;
  /** Stretch each segment to fill the track. The mobile layouts do this. */
  fullWidth?: boolean;
  className?: string;
}

export function SegmentedControl<T extends string>({
  label,
  segments,
  value,
  onChange,
  fullWidth = false,
  className,
}: SegmentedControlProps<T>) {
  // A Map keyed by index rather than an array: assigning into `refs.current[index]` is the exact
  // computed-member write security/detect-object-injection flags, and a Map cannot end up with
  // holes when the segment list changes length.
  const refs = useRef(new Map<number, HTMLButtonElement>());

  function move(from: number, delta: number) {
    const next = (from + delta + segments.length) % segments.length;
    const segment = segments.at(next);
    if (!segment) return;
    onChange(segment.value);
    refs.current.get(next)?.focus();
  }

  function onKeyDown(e: KeyboardEvent<HTMLButtonElement>, index: number) {
    switch (e.key) {
      case "ArrowRight":
      case "ArrowDown":
        e.preventDefault();
        move(index, 1);
        break;
      case "ArrowLeft":
      case "ArrowUp":
        e.preventDefault();
        move(index, -1);
        break;
      case "Home":
        e.preventDefault();
        move(0, 0);
        break;
      case "End":
        e.preventDefault();
        move(segments.length - 1, 0);
        break;
    }
  }

  return (
    <div
      role="radiogroup"
      aria-label={label}
      className={cx(
        "surface-sunk inline-flex gap-px rounded-ctl p-[3px]",
        // Filter sets outgrow a narrow screen. Scrolling the track is the design's answer, not
        // wrapping it, so the control stays one line and one shape.
        "max-w-full overflow-x-auto",
        fullWidth && "flex w-full",
        className,
      )}
    >
      {segments.map((segment, index) => {
        const selected = segment.value === value;
        return (
          <button
            key={segment.value}
            ref={el => {
              if (el) refs.current.set(index, el);
              else refs.current.delete(index);
            }}
            type="button"
            role="radio"
            aria-checked={selected}
            // Roving tabindex: Tab lands on the selected segment and the next Tab leaves the
            // control entirely, rather than walking through every filter.
            tabIndex={selected ? 0 : -1}
            onClick={() => onChange(segment.value)}
            onKeyDown={e => onKeyDown(e, index)}
            className={cx(
              "rounded-inset px-[11px] py-[5px] text-control font-[650] whitespace-nowrap focus-ring tappable",
              "transition-[background-color,color,transform] duration-350 ease-spring motion-reduce:transition-none",
              "active:scale-[.97]",
              fullWidth && "flex-1",
              selected ? "bg-core text-ink shadow-e1" : "text-muted hover:text-ink",
            )}
          >
            {segment.label}
            {segment.count !== undefined && (
              <span className={cx("ml-1.5", selected ? "text-muted" : "text-faint")}>{segment.count}</span>
            )}
          </button>
        );
      })}
    </div>
  );
}
