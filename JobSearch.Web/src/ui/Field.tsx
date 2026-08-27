import { useId, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from "react";
import { cx } from "./cx";
import { ChevronDownIcon } from "./icons";

// Form controls, and the wiring that makes them accessible by construction.
//
// Field owns the ids. A caller cannot forget htmlFor, aria-describedby or aria-invalid, because it
// never writes them — it receives them. That is the entire reason this exists as a component
// rather than as a class string: the previous CardEditor `Field` rendered a bare <label> with no
// htmlFor at all, so clicking a label did nothing and a screen reader read the input unlabelled.
//
// Prop shape intentionally tracks CardEditor's Field (label / value / onChange / multiline) so the
// later swap is a rename plus deleting the old one, not a redesign.

/** The ids Field has generated. Spread the relevant ones onto whatever control you render. */
export interface FieldRenderProps {
  id: string;
  /** undefined when there is neither a hint nor an error, so the attribute is simply absent. */
  "aria-describedby": string | undefined;
  "aria-invalid": true | undefined;
  required: boolean | undefined;
}

export interface FieldProps {
  label: string;
  hint?: string;
  /** Present means invalid. The message replaces the hint rather than stacking under it. */
  error?: string;
  required?: boolean;
  className?: string;
  children: (props: FieldRenderProps) => ReactNode;
}

export function Field({ label, hint, error, required, className, children }: FieldProps) {
  const base = useId();
  const id = `${base}-control`;
  const hintId = `${base}-hint`;
  const errorId = `${base}-error`;
  // The error message describes the field once it exists; before that, the hint does. Pointing at
  // both would have a screen reader read guidance the user has already failed to follow.
  const describedBy = error ? errorId : hint ? hintId : undefined;

  return (
    <div className={cx("min-w-0", className)}>
      <label htmlFor={id} className="mb-[5px] block text-meta font-[650] text-muted">
        {label}
        {required && (
          <span className="ml-1 text-ember" aria-hidden="true">
            *
          </span>
        )}
      </label>

      {children({
        id,
        "aria-describedby": describedBy,
        "aria-invalid": error ? true : undefined,
        required: required || undefined,
      })}

      {error ? (
        // aria-live so a message that appears after the user has moved on is still announced.
        <p id={errorId} aria-live="polite" className="mt-1.5 text-caption text-ember">
          {error}
        </p>
      ) : hint ? (
        <p id={hintId} className="mt-1.5 text-meta text-faint">
          {hint}
        </p>
      ) : null}
    </div>
  );
}

const CONTROL =
  "w-full rounded-ctl bg-sunk px-3 py-[9px] text-body text-ink border-0 placeholder:text-faint focus-ring " +
  "transition-[box-shadow,background-color] duration-400 ease-spring motion-reduce:transition-none";

/** The hairline ring, or an ember one once the field is invalid. */
function ringFor(invalid: boolean) {
  return invalid ? "shadow-[inset_0_0_0_1px_var(--color-ember)]" : "hairline-ring";
}

type NativeInputProps = Omit<InputHTMLAttributes<HTMLInputElement>, "className" | "id">;

export interface InputProps extends NativeInputProps {
  label: string;
  hint?: string;
  error?: string;
  className?: string;
}

/**
 * type / inputMode / autoComplete / spellCheck are passed straight through — the prototype is
 * specific about them (type="url" inputMode="url" autoComplete="off" spellCheck={false} on the
 * posting link, autoComplete="new-password" on registration) and getting them wrong is the
 * difference between a usable mobile keyboard and a hostile one.
 */
export function Input({ label, hint, error, required, className, ...rest }: InputProps) {
  return (
    <Field label={label} hint={hint} error={error} required={required}>
      {field => <input {...field} {...rest} className={cx(CONTROL, ringFor(Boolean(error)), className)} />}
    </Field>
  );
}

type NativeTextareaProps = Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, "className" | "id">;

export interface TextareaProps extends NativeTextareaProps {
  label: string;
  hint?: string;
  error?: string;
  className?: string;
}

export function Textarea({ label, hint, error, required, rows = 3, className, ...rest }: TextareaProps) {
  return (
    <Field label={label} hint={hint} error={error} required={required}>
      {field => <textarea {...field} {...rest} rows={rows} className={cx(CONTROL, ringFor(Boolean(error)), className)} />}
    </Field>
  );
}

type NativeSelectProps = Omit<SelectHTMLAttributes<HTMLSelectElement>, "className" | "id">;

export interface SelectProps extends NativeSelectProps {
  label: string;
  hint?: string;
  error?: string;
  className?: string;
  children: ReactNode;
}

/**
 * The native chevron is suppressed and redrawn from the icon set, because the UA one is neither
 * 1.5-stroke nor theme-aware. The select itself stays a real <select>, so mobile still gets the
 * platform picker.
 */
export function Select({ label, hint, error, required, className, children, ...rest }: SelectProps) {
  return (
    <Field label={label} hint={hint} error={error} required={required}>
      {field => (
        <div className="relative">
          <select {...field} {...rest} className={cx(CONTROL, ringFor(Boolean(error)), "appearance-none pr-9", className)}>
            {children}
          </select>
          <ChevronDownIcon className="pointer-events-none absolute top-1/2 right-3 h-3.5 w-3.5 -translate-y-1/2 text-faint" />
        </div>
      )}
    </Field>
  );
}
