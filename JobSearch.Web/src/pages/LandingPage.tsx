import { useState, type FormEvent, type ReactNode } from "react";
import {
  useLoginUrl,
  usePasswordLogin,
  useRegister,
  useRequestPasswordReset,
  useResetPassword,
} from "../hooks/useAuth";
import { Button, Callout, CheckIcon, Divider, Input, PasswordRulesChecklist, Surface, ThemeToggle, cx, BrandGlyph } from "../ui";
import { isPasswordValid, passwordRuleResults } from "../lib/passwordRules";

const FEATURES = [
  "Tailored CVs and cover letters, grounded in your real background",
  "Every posting evaluated against your own criteria, not a generic score",
  "Track applications and discover new postings automatically",
];

// Google stays a peer sign-in option, never a lesser one — it sits below the same divider on
// both tabs and keeps full width. It's an <a>, not a button, because it's a redirect round-trip
// through Google's consent screen rather than a fetch (see useLoginUrl). Button's own `href`
// prop renders exactly that <a>.

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

// Sign in / Create account switch. Not a SegmentedControl: that component is explicitly a filter
// over a list already on screen (see ui/SegmentedControl.tsx's own note), and this swaps between
// two different forms instead — so it is hand-rolled here, but the track-plus-pill treatment is
// the same visual language SegmentedControl and ThemeToggle use elsewhere.
function TabSwitch({ tab, onChange }: { tab: "signin" | "register"; onChange: (tab: "signin" | "register") => void }) {
  return (
    <div role="group" aria-label="Sign in or create an account" className="surface-sunk mt-6 inline-flex w-full gap-px rounded-ctl p-[3px]">
      {(["signin", "register"] as const).map(value => (
        <button
          key={value}
          type="button"
          aria-pressed={tab === value}
          onClick={() => onChange(value)}
          className={cx(
            "flex-1 rounded-inset px-3 py-1.5 text-control font-[650] tappable focus-ring",
            "transition-[background-color,color,transform] duration-350 ease-spring motion-reduce:transition-none active:scale-[.97]",
            tab === value ? "bg-core text-ink shadow-e1" : "text-muted hover:text-ink",
          )}
        >
          {value === "signin" ? "Sign in" : "Create account"}
        </button>
      ))}
    </div>
  );
}

// Live mirror of the server's own password check (see lib/passwordRules.ts) so the rules are
// visible while typing instead of arriving as a 400 after submitting.
function LivePasswordChecklist({ password }: { password: string }) {
  return (
    <PasswordRulesChecklist
      rules={passwordRuleResults(password).map(rule => ({ id: rule.label, ...rule }))}
      className="mt-1"
    />
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
    <form onSubmit={handleSubmit} className="mt-4 space-y-3 text-left">
      <Input
        label="Email"
        type="email"
        value={email}
        onChange={e => setEmail(e.target.value)}
        autoComplete="email"
        placeholder="you@example.com"
        required
      />
      <Input
        label="Password"
        type="password"
        value={password}
        onChange={e => setPassword(e.target.value)}
        autoComplete="current-password"
        required
      />
      <Button type="submit" disabled={action.loading} loading={action.loading} fullWidth>
        {action.loading ? "Signing in…" : "Sign in"}
      </Button>
      {action.error && <Callout variant="danger" title={action.error} />}
      <p className="text-left">
        <button type="button" onClick={onForgotPassword} className="text-caption font-[650] text-ember hover:text-ember-hi">
          Forgot your password?
        </button>
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
    <form onSubmit={handleSubmit} className="mt-4 space-y-3 text-left">
      <Input
        label="Email"
        type="email"
        value={email}
        onChange={e => setEmail(e.target.value)}
        autoComplete="email"
        placeholder="you@example.com"
        required
      />
      <Input
        label="Password"
        type="password"
        value={password}
        onChange={e => setPassword(e.target.value)}
        autoComplete="new-password"
        required
      />
      <LivePasswordChecklist password={password} />
      <Button type="submit" disabled={action.loading || !isPasswordValid(password)} loading={action.loading} fullWidth>
        {action.loading ? "Creating account…" : "Create account"}
      </Button>
      {action.error && <Callout variant="danger" title={action.error} />}
      <p className="text-caption text-faint">Invite only while in beta.</p>
    </form>
  );
}

function CheckYourInbox({ email, onBack }: { email: string; onBack: () => void }) {
  return (
    <div className="mt-6">
      <div className="mx-auto flex h-11 w-11 items-center justify-center rounded-core bg-shell text-ink-2">
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-5 w-5" aria-hidden="true">
          <path strokeLinecap="round" strokeLinejoin="round" d="M4 4h16v16H4zM4 7l8 6 8-6" />
        </svg>
      </div>
      <h2 className="mt-3 text-heading font-bold text-ink">Check your inbox</h2>
      <p className="mt-1 text-body text-muted">
        We sent a link to <span className="font-[650] text-ink-2">{email}</span>.
        Click it to activate your account. The link expires in an hour.
      </p>
      <button type="button" onClick={onBack} className="mt-4 text-caption font-[650] text-ember hover:text-ember-hi">
        Back to sign in
      </button>
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
    <div className="mt-6 text-left">
      <h2 className="text-heading font-bold text-ink">Forgot your password</h2>
      {sentMessage ? (
        <p className="mt-2 text-body text-muted">{sentMessage}</p>
      ) : (
        <>
          <p className="mt-1 mb-3 text-body text-muted">
            Enter the email you signed up with and we'll send a reset link.
          </p>
          <form onSubmit={handleSubmit} className="space-y-3">
            <Input
              label="Email"
              type="email"
              value={email}
              onChange={e => setEmail(e.target.value)}
              autoComplete="email"
              placeholder="you@example.com"
              required
            />
            <Button type="submit" disabled={action.loading} loading={action.loading} fullWidth>
              {action.loading ? "Sending…" : "Send reset link"}
            </Button>
            {action.error && <Callout variant="danger" title={action.error} />}
          </form>
        </>
      )}
      <button type="button" onClick={onBack} className="mt-4 text-caption font-[650] text-ember hover:text-ember-hi">
        Back to sign in
      </button>
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
    <div className="mt-6 text-left">
      <h2 className="text-heading font-bold text-ink">Set a new password</h2>
      <p className="mt-1 mb-3 text-body text-muted">
        The link works once and expires after an hour.
      </p>
      <form onSubmit={handleSubmit} className="space-y-3">
        <Input
          label="New password"
          type="password"
          value={password}
          onChange={e => setPassword(e.target.value)}
          autoComplete="new-password"
          required
        />
        <LivePasswordChecklist password={password} />
        <Button type="submit" disabled={action.loading || !isPasswordValid(password)} loading={action.loading} fullWidth>
          {action.loading ? "Setting password…" : "Set password and sign in"}
        </Button>
        {action.error && <Callout variant="danger" title={action.error} />}
      </form>
    </div>
  );
}

function FeatureItem({ children }: { children: ReactNode }) {
  return (
    <li className="flex items-start gap-2 text-body text-ink-2">
      <CheckIcon className="mt-0.5 h-4 w-4 shrink-0 text-ember" />
      {children}
    </li>
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
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-bg px-6 py-10">
      <div className="pointer-events-none absolute -top-32 -left-32 h-96 w-96 rounded-full bg-ember/20 blur-3xl" />
      <div className="pointer-events-none absolute -right-32 -bottom-32 h-96 w-96 rounded-full bg-brass/20 blur-3xl" />

      <ThemeToggle className="absolute top-4 right-4" />

      <Surface elevation="floating" padding="none" className="relative w-full max-w-md animate-fade-in-up p-8 text-center">
        <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-core bg-ember text-on-ember">
          <BrandGlyph className="h-6 w-6" />
        </div>

        {/* translate="no" so machine translation doesn't render the wordmark as two English
            nouns — same reasoning as ui/AppShell's Brand, which this page can't reuse directly
            (Brand is nav-bar sized; this is a hero mark). */}
        <h1 translate="no" className="mt-4 text-display font-bold text-ink">Work Santa</h1>

        {focusedView ?? (
          <>
            <p className="mt-2 text-body text-muted">
              Evaluate job postings against your own criteria, and generate tailored CVs, cover
              letters, and application answers grounded in your real background.
            </p>

            <ul className="mt-6 space-y-2 text-left">
              {FEATURES.map(feature => <FeatureItem key={feature}>{feature}</FeatureItem>)}
            </ul>

            {authError && (
              <div className="mt-4 text-left">
                <Callout variant="danger" title={AUTH_ERRORS.get(authError) ?? DEFAULT_AUTH_ERROR} />
              </div>
            )}

            <TabSwitch tab={tab} onChange={setTab} />

            {tab === "signin"
              ? <SignInForm onForgotPassword={() => setForgotPassword(true)} />
              : <RegisterForm onVerificationSent={setPendingVerificationEmail} />}

            <Divider className="my-4">or</Divider>
            <Button href={loginUrl} variant="ghost" fullWidth>Sign in with Google</Button>
          </>
        )}
      </Surface>
    </div>
  );
}
