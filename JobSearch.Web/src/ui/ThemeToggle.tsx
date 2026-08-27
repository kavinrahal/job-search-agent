import { cx } from "./cx";
import { useTheme, type ThemePreference } from "./ThemeProvider";
import { MonitorIcon, MoonIcon, SunIcon } from "./icons";

// The theme control from the prototype's top bar: a pill-shaped three-way segment.
//
// Three options rather than the existing ThemeToggle's two, because "system" is a real preference
// and a two-state toggle silently forces everyone off it the first time they touch it. Icons
// rather than words, since it sits in a 50px bar next to the credit pill and the avatar.
//
// Pill-shaped, unlike SegmentedControl's 9px track, matching the prototype — the theme control is
// chrome, not a content filter, and the different shape is what keeps it from being read as one.

const OPTIONS: Array<{ value: ThemePreference; label: string; Icon: typeof SunIcon }> = [
  { value: "light", label: "Light", Icon: SunIcon },
  { value: "dark", label: "Dark", Icon: MoonIcon },
  { value: "system", label: "Match system", Icon: MonitorIcon },
];

export function ThemeToggle({ className }: { className?: string }) {
  const { preference, setPreference } = useTheme();

  return (
    <div role="group" aria-label="Colour theme" className={cx("hairline-ring inline-flex gap-px rounded-pill bg-shell p-[3px]", className)}>
      {OPTIONS.map(({ value, label, Icon }) => {
        const active = preference === value;
        return (
          <button
            key={value}
            type="button"
            // aria-pressed rather than a radiogroup: each button is independently toggleable and
            // the prototype's own control announced itself this way.
            aria-pressed={active}
            aria-label={label}
            onClick={() => setPreference(value)}
            className={cx(
              "grid h-6 w-7 place-items-center rounded-pill focus-ring tappable",
              "transition-[background-color,color,transform] duration-350 ease-spring motion-reduce:transition-none",
              "active:scale-[.94]",
              active ? "bg-core text-ink shadow-e1" : "text-muted hover:text-ink",
            )}
          >
            <Icon className="h-3.5 w-3.5" />
          </button>
        );
      })}
    </div>
  );
}
