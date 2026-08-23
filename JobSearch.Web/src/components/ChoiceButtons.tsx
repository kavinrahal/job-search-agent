// Generic single/multi-select button-group — the primitive behind the onboarding criteria
// wizard's multiple-choice questions. Active/inactive classes match the employment-type toggle
// buttons that already existed inline in JobCriteriaEditor.tsx, so this introduces no visual
// change there, only a shared implementation.
export interface ChoiceOption<T extends string = string> {
  value: T;
  label: string;
}

type ChoiceButtonsProps<T extends string> =
  | { multi?: false; options: ChoiceOption<T>[]; value: T | null; onChange: (v: T) => void; className?: string }
  | { multi: true; options: ChoiceOption<T>[]; value: T[]; onChange: (v: T[]) => void; className?: string };

export function ChoiceButtons<T extends string>(props: ChoiceButtonsProps<T>) {
  const isActive = (v: T) => (props.multi ? props.value.includes(v) : props.value === v);

  function handleClick(v: T) {
    if (props.multi) {
      props.onChange(props.value.includes(v) ? props.value.filter(x => x !== v) : [...props.value, v]);
    } else {
      props.onChange(v);
    }
  }

  return (
    <div className={`flex flex-wrap gap-2 ${props.className ?? ""}`} role={props.multi ? "group" : "radiogroup"}>
      {props.options.map(opt => (
        <button
          key={opt.value}
          type="button"
          role={props.multi ? undefined : "radio"}
          aria-checked={!props.multi ? isActive(opt.value) : undefined}
          aria-pressed={props.multi ? isActive(opt.value) : undefined}
          onClick={() => handleClick(opt.value)}
          className={`rounded-lg px-3 py-1.5 text-sm font-medium transition-colors duration-150 focus:outline-none focus-visible:ring-2 focus-visible:ring-violet-400 ${
            isActive(opt.value)
              ? "bg-violet-50 text-violet-700 dark:bg-violet-500/15 dark:text-violet-300"
              : "bg-gray-100 text-gray-500 hover:bg-gray-200 dark:bg-gray-800 dark:text-gray-400 dark:hover:bg-gray-700"
          }`}
        >
          {opt.label}
        </button>
      ))}
    </div>
  );
}
