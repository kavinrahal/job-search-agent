import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import {
  useLoginUrl,
  usePasswordLogin,
  useRegister,
  useRequestPasswordReset,
  useResetPassword,
} from "../hooks/useAuth";
import {
  Brand,
  Button,
  Callout,
  Divider,
  Input,
  PasswordRulesChecklist,
  StatusTick,
  Surface,
  ThemeToggle,
  cx,
} from "../ui";
import { isPasswordValid, passwordRuleResults } from "../lib/passwordRules";

// The sign-in / create-account / forgot / reset screen (prototype sections 12–14). A split card:
// the form on the left, a dark "last night" proof panel on the right that drops on mobile so the
// form gets the full screen.
//
// The auth *logic* here is unchanged from the old single-card LandingPage — same hooks, same
// hard-navigate-home behaviour. This file only gives it the prototype's shell and wires the tab
// switch to real /signin and /register routes.

type AuthTab = "signin" | "register";

// Hard navigation, not a client-side route: every one of these flows has just established a fresh
// session cookie server-side, and useMe() only fetches /auth/me once on mount. Nothing short of a
// full page load will notice the user is now signed in — App would otherwise keep rendering the
// logged-out routes off its stale null `me`.
function hardNavigateHome() {
  window.location.href = "/";
}

// Set by the API when it redirects back here after a failed auth attempt: Google's OnRemoteFailure
// (not invited, deactivated) and /auth/verify-email with a token it wouldn't accept.
// A Map rather than an object keyed by the raw param, so an attacker-supplied ?authError= can't
// reach Object.prototype keys (eslint-plugin-security's object-injection sink).
const AUTH_ERRORS = new Map<string, string>([
  [
    "invalid_token",
    "That account activation link is invalid, expired, or has already been used — they only last an hour. Reach out to whoever invited you for a fresh one.",
  ],
]);
const DEFAULT_AUTH_ERROR =
  "That Google account needs an invite before it can sign in. Ask whoever invited you, or reach out via the Support page once you're in.";

// Sign in / Create account switch. Not a SegmentedControl: that component is explicitly a filter
// over a list already on screen, and this swaps between two routes instead — so it is hand-rolled
// here, but the track-plus-pill treatment is the same visual language SegmentedControl uses.
function TabSwitch({ tab }: { tab: AuthTab }) {
  const navigate = useNavigate();
  return (
    <div role="group" aria-label="Sign in or create an account" className="surface-sunk flex w-full gap-px rounded-ctl p-[3px] md:w-auto md:self-start">
      {(["signin", "register"] as const).map(value => (
        <button
          key={value}
          type="button"
          aria-pressed={tab === value}
          onClick={() => navigate(value === "signin" ? "/signin" : "/register")}
          className={cx(
            "flex-1 rounded-inset px-3 py-1.5 text-control font-[650] tappable focus-ring md:flex-none",
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
      <Button type="submit" cap disabled={action.loading} loading={action.loading} fullWidth>
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
    // "signed_in" means this email had already proved ownership via a prior Google login, so the
    // server attached the password to that same account and signed them straight in — no
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
      <Button type="submit" cap disabled={action.loading || !isPasswordValid(password)} loading={action.loading} fullWidth>
        {action.loading ? "Creating account…" : "Create account"}
      </Button>
      {action.error && <Callout variant="danger" title={action.error} />}
      <p className="text-caption text-faint">Invite only while in beta.</p>
    </form>
  );
}

function CheckYourInbox({ email, onBack }: { email: string; onBack: () => void }) {
  return (
    <div className="mt-2 text-left">
      <div className="flex h-11 w-11 items-center justify-center rounded-core bg-shell text-ink-2">
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-5 w-5" aria-hidden="true">
          <path strokeLinecap="round" strokeLinejoin="round" d="M4 4h16v16H4zM4 7l8 6 8-6" />
        </svg>
      </div>
      <h2 className="mt-3 text-heading font-bold text-ink">Check your inbox</h2>
      <p className="mt-1 text-body text-muted">
        We sent a link to <span className="font-[650] text-ink-2">{email}</span>. Click it to
        activate your account. The link expires in an hour.
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
      // Deliberately generic and identical whether or not that email has an account — the server
      // won't confirm either way, so neither does this screen.
      setSentMessage(result.message);
    } catch {
      // Already surfaced via action.error below.
    }
  }

  return (
    <div className="text-left">
      <p className="text-eyebrow text-faint uppercase">Step one</p>
      <h2 className="mt-2.5 text-heading font-bold text-ink">Forgot your password</h2>
      {sentMessage ? (
        <p className="mt-2 text-body text-muted">{sentMessage}</p>
      ) : (
        <>
          <p className="mt-1 mb-3 text-body text-muted">Enter the email you signed up with and we'll send a reset link.</p>
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
            <Button type="submit" cap disabled={action.loading} loading={action.loading} fullWidth>
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

// Reached from the emailed reset link (/?resetToken=...). The server signs the user in as part of
// a successful reset, so this lands them straight in the app.
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
    hardNavigateHome();
  }

  return (
    <div className="text-left">
      <p className="text-eyebrow text-faint uppercase">Step two</p>
      <h2 className="mt-2.5 text-heading font-bold text-ink">Set a new password</h2>
      <p className="mt-1 mb-3 text-body text-muted">The link works once and expires after an hour.</p>
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
        <Button type="submit" cap disabled={action.loading || !isPasswordValid(password)} loading={action.loading} fullWidth>
          {action.loading ? "Setting password…" : "Set password and sign in"}
        </Button>
        {action.error && <Callout variant="danger" title={action.error} />}
      </form>
    </div>
  );
}

// The dark proof panel on the right of the card. Stays dark in both themes on purpose (same
// principle as FeaturePanel) — it is the product talking, a figure against the ground. Drops on
// mobile so the form gets the full screen.
const LAST_NIGHT = ["Victorian Government", "GHD"];

function ProofPanel() {
  return (
    <div className="relative hidden overflow-hidden bg-feat px-[34px] py-[38px] text-feat-ink md:flex md:flex-col md:justify-center">
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-0"
        style={{ background: "radial-gradient(115% 85% at 88% 6%, rgba(255,255,255,.11), transparent 55%)" }}
      />
      <div className="relative">
        <p className="text-eyebrow text-feat-dim uppercase">Last night</p>
        <h2 className="mt-2 mb-3.5 text-display font-bold text-balance">While you were asleep, five postings were checked.</h2>
        <div className="flex flex-col gap-[7px]">
          {LAST_NIGHT.map(company => (
            <div key={company} className="flex items-center gap-2.5 rounded-ctl bg-white/[.07] px-[11px] py-2">
              <StatusTick state="done" size="sm" />
              <span className="text-note font-[600]">{company}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

export interface AuthPageProps {
  initialTab: AuthTab;
  /** Present when the emailed reset link (/?resetToken=…) brought the user here. */
  resetToken?: string | null;
  /** Present when the API bounced a failed Google / activation attempt back with ?authError=. */
  authError?: string | null;
}

export function AuthPage({ initialTab, resetToken, authError }: AuthPageProps) {
  const navigate = useNavigate();
  const loginUrl = useLoginUrl();
  const [forgotPassword, setForgotPassword] = useState(false);
  const [pendingVerificationEmail, setPendingVerificationEmail] = useState<string | null>(null);

  function backToSignIn() {
    setForgotPassword(false);
    setPendingVerificationEmail(null);
    navigate("/signin");
  }

  // Each of these takes over the whole form side: they're end states or email-link destinations,
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
      <div className="pointer-events-none absolute -top-32 -left-32 h-96 w-96 rounded-pill bg-ember/20 blur-3xl" />
      <div className="pointer-events-none absolute -right-32 -bottom-32 h-96 w-96 rounded-pill bg-brass/20 blur-3xl" />

      <ThemeToggle className="absolute top-4 right-4 z-1" />

      <Surface elevation="floating" padding="none" clip className="relative w-full max-w-[880px] animate-fade-in-up">
        <div className="grid md:grid-cols-2">
          <div className="flex flex-col justify-center px-[34px] py-[38px] text-left">
            <div className="mb-5">
              <Brand />
            </div>

            {focusedView ?? (
              <>
                <TabSwitch tab={initialTab} />

                {authError && (
                  <div className="mt-4 text-left">
                    <Callout variant="danger" title={AUTH_ERRORS.get(authError) ?? DEFAULT_AUTH_ERROR} />
                  </div>
                )}

                {initialTab === "signin" ? (
                  <SignInForm onForgotPassword={() => setForgotPassword(true)} />
                ) : (
                  <RegisterForm onVerificationSent={setPendingVerificationEmail} />
                )}

                <Divider className="my-4">or</Divider>
                <Button href={loginUrl} variant="ghost" fullWidth>
                  {initialTab === "signin" ? "Continue with Google" : "Sign in with Google"}
                </Button>
              </>
            )}
          </div>

          <ProofPanel />
        </div>
      </Surface>
    </div>
  );
}
