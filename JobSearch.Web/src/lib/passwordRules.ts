// Client-side mirror of JobSearch.Data/PasswordRules.cs — UX only, never the gate. The server
// re-runs the identical check on /auth/register and /auth/reset-password and is the only thing
// that actually decides; this exists so the register/reset forms can show a live checklist
// instead of making the user guess and round-trip.
//
// The two implementations must agree, so `message` below is a verbatim copy of each string
// PasswordRules.Validate() adds, in the same order — passwordRules.test.ts reads the real C#
// file and fails if either side is edited without the other. `label` is the short form the
// compact checklist grid renders; it has no server counterpart.
//
// Unicode note: the .NET side works on UTF-16 chars via char.IsLower/IsUpper/IsDigit/
// IsLetterOrDigit, which map to the Unicode categories Ll / Lu / Nd / (L or Nd) respectively —
// hence the \p{...} classes rather than the ASCII-only [a-z]/[A-Z]/[0-9]/[^\w] shorthands,
// which would quietly reject passwords the server accepts. The one residual difference is
// astral-plane characters (JS matches them by code point, .NET sees lone surrogates), which
// only ever makes the client stricter than the server, never looser.

export const MIN_PASSWORD_LENGTH = 8;

export interface PasswordRule {
  /** Short checklist label, e.g. "Uppercase". Client-only. */
  label: string;
  /** Verbatim copy of the server's failure message for this rule. */
  message: string;
  test: (password: string) => boolean;
}

export const PASSWORD_RULES: readonly PasswordRule[] = [
  {
    label: `${MIN_PASSWORD_LENGTH} characters`,
    message: `Must be at least ${MIN_PASSWORD_LENGTH} characters.`,
    test: p => p.length >= MIN_PASSWORD_LENGTH,
  },
  {
    label: "Lowercase",
    message: "Must include a lowercase letter.",
    test: p => /\p{Ll}/u.test(p),
  },
  {
    label: "Uppercase",
    message: "Must include an uppercase letter.",
    test: p => /\p{Lu}/u.test(p),
  },
  {
    label: "A number",
    message: "Must include a number.",
    test: p => /\p{Nd}/u.test(p),
  },
  {
    label: "A symbol",
    message: "Must include a special character.",
    // The complement of .NET's char.IsLetterOrDigit — anything that is neither a letter of
    // any case/script nor a decimal digit.
    test: p => /[^\p{L}\p{Nd}]/u.test(p),
  },
];

export interface PasswordRuleResult {
  label: string;
  met: boolean;
}

/** Every rule with its current pass/fail state — drives the live checklist while typing. */
export function passwordRuleResults(password: string): PasswordRuleResult[] {
  return PASSWORD_RULES.map(rule => ({ label: rule.label, met: rule.test(password) }));
}

/** True when the server would accept this password. Gates the submit button. */
export function isPasswordValid(password: string): boolean {
  return PASSWORD_RULES.every(rule => rule.test(password));
}
