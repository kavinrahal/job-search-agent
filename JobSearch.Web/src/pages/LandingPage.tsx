import { useState, type FormEvent, type ReactNode } from "react";
import {
  useLoginUrl,
  usePasswordLogin,
  useRegister,
  useRequestPasswordReset,
  useResetPassword,
} from "../hooks/useAuth";
import { ThemeToggle } from "../components/ThemeToggle";
import { INPUT, PRIMARY_BUTTON } from "../lib/styles";
import { isPasswordValid, passwordRuleResults } from "../lib/passwordRules";

const FEATURES = [
  "Tailored CVs and cover letters, grounded in your real background",
  "Every posting evaluated against your own criteria, not a generic score",
  "Track applications and discover new postings automatically",
];

// Google stays a peer sign-in option, never a lesser one — it sits below the same "or" divider
// on both tabs and keeps full width. It's an <a>, not a button, because it's a redirect
// round-trip through Google's consent screen rather than a fetch (see useLoginUrl).
const GOOGLE_BUTTON =
  "inline-flex w-full items-center justify-center rounded-lg border border-gray-300 bg-white px-4 py-2.5 text-sm font-medium text-gray-700 shadow-sm transition-colors duration-150 hover:bg-gray-50 dark:border-gray-700 dark:bg-gray-800 dark:text-gray-100 dark:hover:bg-gray-700";

const SUBMIT_BUTTON = `${PRIMARY_BUTTON} w-full py-2.5`;

const LINK = "font-medium text-violet-600 transition-colors hover:text-violet-700 dark:text-violet-400 dark:hover:text-violet-300";

const ERROR_TEXT = "mt-2 text-sm text-red-700 dark:text-red-400";

// Plain top-level function rather than an inline `window.location.href = ...` inside the
// components below — same reason SourcesPage extracts one: react-hooks' bundled compiler
// diagnostics flag the direct assignment when it's textually inside a component that also
// calls a useState setter, and it surfaces as a bare compiler error no eslint-disable can
// suppress.
//
// Hard navigation, not a client-side route: every one of these flows has just established a
// fresh session cookie server-side, and useMe() only fetches /auth/me once on mount (same
// reasoning as ResumeIntakePage.handleSave). Nothing short of a full page load will notice
// the user is now signed in — App.tsx would otherwise keep rendering this landing page off
// its stale null `me`.
function hardNavigateHome() {
  window.location.href = "/";
}

// Set by the API when it redirects back here after a failed auth attempt: Google's
// OnRemoteFailure (not invited, deactivated) and, since the password PR, /auth/verify-email
// with a token it wouldn't accept.
// A Map rather than an object keyed by the raw param, so an attacker-supplied ?authError=
// can't reach Object.prototype keys (eslint-plugin-security's object-injection sink).
const AUTH_ERRORS = new Map<string, string>([
  [
    "invalid_token",
    "That account activation link is invalid, expired, or has already been used — they only last an hour. Reach out to whoever invited you for a fresh one.",
  ],
]);
const DEFAULT_AUTH_ERROR =
  "That Google account needs an invite before it can sign in. Ask whoever invited you, or reach out via the Support page once you're in.";

function TabButton({ active, onClick, children }: { active: boolean; onClick: () => void; children: ReactNode }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`flex-1 rounded-lg px-3 py-1.5 text-sm font-medium transition-colors duration-150 ${
        active
          ? "bg-violet-50 text-violet-700 dark:bg-violet-500/15 dark:text-violet-300"
          : "text-gray-500 hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-800"
      }`}
    >
      {children}
    </button>
  );
}

function Field({
  id, label, type, value, onChange, autoComplete, placeholder,
}: {
  id: string;
  label: string;
  type: "email" | "password";
  value: string;
  onChange: (value: string) => void;
  autoComplete: string;
  placeholder?: string;
}) {
  return (
    <div className="text-left">
      <label htmlFor={id} className="mb-1 block text-xs font-medium text-gray-600 dark:text-gray-300">
        {label}
      </label>
      <input
        id={id}
        name={id}
        type={type}
        value={value}
        onChange={e => onChange(e.target.value)}
        autoComplete={autoComplete}
        placeholder={placeholder}
        spellCheck={false}
        required
        className={INPUT}
      />
    </div>
  );
}

function CheckIcon({ className }: { className: string }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2.5} className={className} aria-hidden="true">
      <path strokeLinecap="round" strokeLinejoin="round" d="M20 6 9 17l-5-5" />
    </svg>
  );
}

// Live mirror of the server's own password check (see lib/passwordRules.ts) so the rules are
// visible while typing instead of arriving as a 400 after submitting.
function PasswordChecklist({ password }: { password: string }) {
  return (
    <ul className="grid grid-cols-2 gap-x-3 gap-y-1 rounded-lg border border-gray-200 bg-gray-50 p-2.5 text-left dark:border-gray-700 dark:bg-gray-800/50">
      {passwordRuleResults(password).map(rule => (
        <li
          key={rule.label}
          className={`flex items-center gap-1.5 text-xs ${
            rule.met ? "text-emerald-600 dark:text-emerald-400" : "text-gray-400 dark:text-gray-500"
          }`}
        >
          {rule.met ? (
            <CheckIcon className="h-3 w-3 shrink-0" />
          ) : (
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-3 w-3 shrink-0" aria-hidden="true">
              <circle cx="12" cy="12" r="9" />
            </svg>
          )}
          {rule.label}
          <span className="sr-only">{rule.met ? " — met" : " — not met yet"}</span>
        </li>
      ))}
    </ul>
  );
}

function OrDivider() {
  return (
    <div className="my-4 flex items-center gap-3">
      <span className="h-px flex-1 bg-gray-200 dark:bg-gray-700" />
      <span className="text-xs text-gray-400 dark:text-gray-500">or</span>
      <span className="h-px flex-1 bg-gray-200 dark:bg-gray-700" />
    </div>
  );
}

function SignInForm({ onForgotPassword }: { onForgotPassword: () => void }) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const action = usePasswordLogin();

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    // useAsyncAction re-throws after recording the message on action.error, which is already
    // rendered below — swallow it here rather than leaving an unhandled rejection.
    try {
      await action.execute(email.trim(), password);
    } catch {
      return;
    }
    hardNavigateHome();
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-3">
      <Field id="signin-email" label="Email" type="email" value={email} onChange={setEmail} autoComplete="email" placeholder="you@example.com" />
      <Field id="signin-password" label="Password" type="password" value={password} onChange={setPassword} autoComplete="current-password" />
      <button type="submit" disabled={action.loading} className={SUBMIT_BUTTON}>
        {action.loading ? "Signing in…" : "Sign in"}
      </button>
      {action.error && <p className={ERROR_TEXT}>{action.error}</p>}
      <p className="text-left text-xs">
        <button type="button" onClick={onForgotPassword} className={LINK}>Forgot your password?</button>
      </p>
    </form>
  );
}

function RegisterForm({ onVerificationSent }: { onVerificationSent: (email: string) => void }) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const action = useRegister();

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const trimmed = email.trim();
    let result;
    try {
      result = await action.execute(trimmed, password);
    } catch {
      return;
    }
    // "signed_in" means this email had already proved ownership via a prior Google login, so
    // the server attached the password to that same account and signed them straight in — no
    // verification email, nothing to wait for.
    if (result.status === "signed_in") hardNavigateHome();
    else onVerificationSent(trimmed);
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-3">
      <Field id="register-email" label="Email" type="email" value={email} onChange={setEmail} autoComplete="email" placeholder="you@example.com" />
      <Field id="register-password" label="Password" type="password" value={password} onChange={setPassword} autoComplete="new-password" />
      <PasswordChecklist password={password} />
      <button type="submit" disabled={action.loading || !isPasswordValid(password)} className={SUBMIT_BUTTON}>
        {action.loading ? "Creating account…" : "Create account"}
      </button>
      {action.error && <p className={ERROR_TEXT}>{action.error}</p>}
      <p className="text-left text-xs text-gray-400 dark:text-gray-500">Invite only while in beta.</p>
    </form>
  );
}

function CheckYourInbox({ email, onBack }: { email: string; onBack: () => void }) {
  return (
    <div>
      <div className="mx-auto flex h-11 w-11 items-center justify-center rounded-xl bg-violet-50 text-violet-600 dark:bg-violet-500/15 dark:text-violet-300">
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-5 w-5" aria-hidden="true">
          <path strokeLinecap="round" strokeLinejoin="round" d="M4 4h16v16H4zM4 7l8 6 8-6" />
        </svg>
      </div>
      <h2 className="mt-3 text-base font-semibold text-gray-900 dark:text-white">Check your inbox</h2>
      <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
        We sent a link to <span className="font-medium text-gray-700 dark:text-gray-200">{email}</span>.
        Click it to activate your account. The link expires in an hour.
      </p>
      <button type="button" onClick={onBack} className={`mt-4 text-xs ${LINK}`}>Back to sign in</button>
    </div>
  );
}

function ForgotPasswordForm({ onBack }: { onBack: () => void }) {
  const [email, setEmail] = useState("");
  const [sentMessage, setSentMessage] = useState<string | null>(null);
  const action = useRequestPasswordReset();

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    try {
      const result = await action.execute(email.trim());
      // Deliberately generic and identical whether or not that email has an account — the
      // server won't confirm either way, so neither does this screen.
      setSentMessage(result.message);
    } catch {
      // Already surfaced via action.error below.
    }
  }

  return (
    <div className="text-left">
      <h2 className="text-base font-semibold text-gray-900 dark:text-white">Forgot your password</h2>
      {sentMessage ? (
        <p className="mt-2 text-sm text-gray-500 dark:text-gray-400">{sentMessage}</p>
      ) : (
        <>
          <p className="mt-1 mb-3 text-sm text-gray-500 dark:text-gray-400">
            Enter the email you signed up with and we'll send a reset link.
          </p>
          <form onSubmit={handleSubmit} className="space-y-3">
            <Field id="forgot-email" label="Email" type="email" value={email} onChange={setEmail} autoComplete="email" placeholder="you@example.com" />
            <button type="submit" disabled={action.loading} className={SUBMIT_BUTTON}>
              {action.loading ? "Sending…" : "Send reset link"}
            </button>
            {action.error && <p className={ERROR_TEXT}>{action.error}</p>}
          </form>
        </>
      )}
      <button type="button" onClick={onBack} className={`mt-4 text-xs ${LINK}`}>Back to sign in</button>
    </div>
  );
}

// Reached from the emailed reset link (/?resetToken=...), which is why this replaces the whole
// card rather than being another tab — there's no pre-login router to give it a route of its
// own (App.tsx only mounts BrowserRouter once useMe() resolves a user).
function ResetPasswordForm({ token }: { token: string }) {
  const [password, setPassword] = useState("");
  const action = useResetPassword();

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    try {
      await action.execute(token, password);
    } catch {
      return;
    }
    // The server signs the user in as part of a successful reset, so this lands them straight
    // in the app — and drops the now-consumed token from the URL on the way.
    hardNavigateHome();
  }

  return (
    <div className="text-left">
      <h2 className="text-base font-semibold text-gray-900 dark:text-white">Set a new password</h2>
      <p className="mt-1 mb-3 text-sm text-gray-500 dark:text-gray-400">
        The link works once and expires after an hour.
      </p>
      <form onSubmit={handleSubmit} className="space-y-3">
        <Field id="reset-password" label="New password" type="password" value={password} onChange={setPassword} autoComplete="new-password" />
        <PasswordChecklist password={password} />
        <button type="submit" disabled={action.loading || !isPasswordValid(password)} className={SUBMIT_BUTTON}>
          {action.loading ? "Setting password…" : "Set password and sign in"}
        </button>
        {action.error && <p className={ERROR_TEXT}>{action.error}</p>}
      </form>
    </div>
  );
}

// No navbar exists pre-login (App.tsx's header only renders once authenticated), so this
// gets its own small corner toggle rather than going without — dark mode should be reachable
// before signing in too, not just after.
export function LandingPage() {
  const params = new URLSearchParams(window.location.search);
  // Both one-off params the API redirects back here with, read the same way authError already
  // was before password auth existed — no pre-login router to route them properly.
  const authError = params.get("authError");
  const resetToken = params.get("resetToken");

  const loginUrl = useLoginUrl();
  const [tab, setTab] = useState<"signin" | "register">("signin");
  const [forgotPassword, setForgotPassword] = useState(false);
  const [pendingVerificationEmail, setPendingVerificationEmail] = useState<string | null>(null);

  function backToSignIn() {
    setForgotPassword(false);
    setPendingVerificationEmail(null);
    setTab("signin");
  }

  // Each of these takes over the whole card: they're end states or email-link destinations,
  // not something you toggle between with the tabs.
  const focusedView = resetToken ? (
    <ResetPasswordForm token={resetToken} />
  ) : pendingVerificationEmail ? (
    <CheckYourInbox email={pendingVerificationEmail} onBack={backToSignIn} />
  ) : forgotPassword ? (
    <ForgotPasswordForm onBack={backToSignIn} />
  ) : null;

  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-gray-50 px-6 py-10 dark:bg-gray-950">
      <div className="pointer-events-none absolute -left-32 -top-32 h-96 w-96 rounded-full bg-violet-400/30 blur-3xl dark:bg-violet-600/20" />
      <div className="pointer-events-none absolute -bottom-32 -right-32 h-96 w-96 rounded-full bg-fuchsia-400/30 blur-3xl dark:bg-fuchsia-600/20" />

      <ThemeToggle className="absolute right-4 top-4" />

      <div className="relative w-full max-w-md animate-fade-in-up rounded-2xl border border-gray-200 bg-white/90 p-8 text-center shadow-xl shadow-gray-900/5 backdrop-blur-sm dark:border-gray-800 dark:bg-gray-900/90 dark:shadow-none">
        <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-xl bg-gradient-to-br from-violet-600 to-fuchsia-500 text-white shadow-sm shadow-violet-600/30">
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2.25} className="h-6 w-6" aria-hidden="true">
            <path strokeLinecap="round" strokeLinejoin="round" d="M13 2 3 14h7l-1 8 10-12h-7l1-8z" />
          </svg>
        </div>

        <h1 className="mt-4 text-2xl font-semibold tracking-tight text-gray-900 dark:text-white">Work Santa</h1>

        {focusedView ? (
          <div className="mt-6">{focusedView}</div>
        ) : (
          <>
            <p className="mt-2 text-sm text-gray-500 dark:text-gray-400">
              Evaluate job postings against your own criteria, and generate tailored CVs, cover
              letters, and application answers grounded in your real background.
            </p>

            <ul className="mt-6 space-y-2 text-left">
              {FEATURES.map(feature => (
                <li key={feature} className="flex items-start gap-2 text-sm text-gray-600 dark:text-gray-300">
                  <CheckIcon className="mt-0.5 h-4 w-4 shrink-0 text-violet-600 dark:text-violet-400" />
                  {feature}
                </li>
              ))}
            </ul>

            {authError && (
              <p className="mt-4 rounded-lg border border-red-200 bg-red-50 p-3 text-left text-sm text-red-700 dark:border-red-900/50 dark:bg-red-950/30 dark:text-red-400">
                {AUTH_ERRORS.get(authError) ?? DEFAULT_AUTH_ERROR}
              </p>
            )}

            <div className="mt-6 flex gap-1 rounded-lg bg-gray-100/70 p-1 dark:bg-gray-800/50">
              <TabButton active={tab === "signin"} onClick={() => setTab("signin")}>Sign in</TabButton>
              <TabButton active={tab === "register"} onClick={() => setTab("register")}>Create account</TabButton>
            </div>

            <div className="mt-4">
              {tab === "signin"
                ? <SignInForm onForgotPassword={() => setForgotPassword(true)} />
                : <RegisterForm onVerificationSent={setPendingVerificationEmail} />}
            </div>

            <OrDivider />
            <a href={loginUrl} className={GOOGLE_BUTTON}>Sign in with Google</a>
          </>
        )}
      </div>
    </div>
  );
}
