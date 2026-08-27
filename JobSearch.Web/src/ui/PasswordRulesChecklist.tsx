import { cx } from "./cx";
import { CheckIcon, CircleIcon } from "./icons";

// The live password rule list on the registration screen.
//
// Presentational only, by design. It takes an already-evaluated array and renders it. The rules
// themselves — what they are, how they are checked, what the backend enforces — belong in
// lib/passwordRules.ts, which the auth work owns. Two modules deciding what a valid password is
// would be one module too many, and the one that is wrong would be this one.

export interface PasswordRuleState {
  /** Stable key. */
  id: string;
  /** Short enough for a two-column grid: "8 characters", "Uppercase", "A number". */
  label: string;
  met: boolean;
}

export interface PasswordRulesChecklistProps {
  rules: PasswordRuleState[];
  className?: string;
}

export function PasswordRulesChecklist({ rules, className }: PasswordRulesChecklistProps) {
  const unmet = rules.filter(rule => !rule.met).length;

  return (
    <div className={cx("surface-sunk rounded-ctl px-[11px] py-2.5", className)}>
      <ul className="m-0 grid list-none grid-cols-2 gap-x-3 gap-y-[5px] p-0">
        {rules.map(rule => (
          <li key={rule.id} className={cx("flex items-center gap-1.5 text-meta", rule.met ? "text-pos" : "text-faint")}>
            {rule.met ? (
              // Heavier than the system's 1.5 because at 11px a 1.5 tick reads as a smudge.
              <CheckIcon strokeWidth={2.6} className="h-[11px] w-[11px] flex-none" />
            ) : (
              <CircleIcon className="h-[11px] w-[11px] flex-none" />
            )}
            {rule.label}
          </li>
        ))}
      </ul>
      {/* One polite summary rather than an aria-live on every row: a live region per rule would
          announce four changes for one keystroke. Screen reader users get "2 requirements left",
          which is the fact they actually need. */}
      <p aria-live="polite" className="sr-only">
        {unmet === 0 ? "All password requirements met." : `${unmet} password ${unmet === 1 ? "requirement" : "requirements"} left.`}
      </p>
    </div>
  );
}
