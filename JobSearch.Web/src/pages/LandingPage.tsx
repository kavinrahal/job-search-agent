import { useLoginUrl } from "../hooks/useAuth";
import { ThemeToggle } from "../components/ThemeToggle";

const FEATURES = [
  "Tailored CVs and cover letters, grounded in your real background",
  "Every posting evaluated against your own criteria, not a generic score",
  "Track applications and discover new postings automatically",
];

// No navbar exists pre-login (App.tsx's header only renders once authenticated), so this
// gets its own small corner toggle rather than going without — dark mode should be reachable
// before signing in too, not just after.
export function LandingPage() {
  const loginUrl = useLoginUrl();
  // Set by the API's OnRemoteFailure redirect when Google sign-in itself fails (not invited,
  // account deactivated, etc.) — without this, a denied sign-in silently dumped you back here
  // with no explanation at all, indistinguishable from just not having signed in yet.
  const authError = new URLSearchParams(window.location.search).get("authError");

  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-gray-50 px-6 dark:bg-gray-950">
      <div className="pointer-events-none absolute -left-32 -top-32 h-96 w-96 rounded-full bg-violet-400/30 blur-3xl dark:bg-violet-600/20" />
      <div className="pointer-events-none absolute -bottom-32 -right-32 h-96 w-96 rounded-full bg-fuchsia-400/30 blur-3xl dark:bg-fuchsia-600/20" />

      <ThemeToggle className="absolute right-4 top-4" />

      <div className="relative w-full max-w-md animate-fade-in-up rounded-2xl border border-gray-200 bg-white/90 p-8 text-center shadow-xl shadow-gray-900/5 backdrop-blur-sm dark:border-gray-800 dark:bg-gray-900/90 dark:shadow-none">
        <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-xl bg-gradient-to-br from-violet-600 to-fuchsia-500 text-white shadow-sm shadow-violet-600/30">
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2.25} className="h-6 w-6">
            <path strokeLinecap="round" strokeLinejoin="round" d="M13 2 3 14h7l-1 8 10-12h-7l1-8z" />
          </svg>
        </div>

        <h1 className="mt-4 text-2xl font-semibold tracking-tight text-gray-900 dark:text-white">Work Santa</h1>
        <p className="mt-2 text-sm text-gray-500 dark:text-gray-400">
          Evaluate job postings against your own criteria, and generate tailored CVs, cover
          letters, and application answers grounded in your real background.
        </p>

        <ul className="mt-6 space-y-2 text-left">
          {FEATURES.map(feature => (
            <li key={feature} className="flex items-start gap-2 text-sm text-gray-600 dark:text-gray-300">
              <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2.5} className="mt-0.5 h-4 w-4 shrink-0 text-violet-600 dark:text-violet-400">
                <path strokeLinecap="round" strokeLinejoin="round" d="M20 6 9 17l-5-5" />
              </svg>
              {feature}
            </li>
          ))}
        </ul>

        {authError && (
          <p className="mt-4 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-950/30 dark:text-red-400">
            That Google account needs an invite before it can sign in. Ask whoever invited
            you, or reach out via the Support page once you're in.
          </p>
        )}

        <a
          href={loginUrl}
          className="mt-6 inline-flex w-full items-center justify-center rounded-lg bg-gradient-to-r from-violet-600 to-fuchsia-500 px-4 py-2.5 text-sm font-medium text-white shadow-sm shadow-violet-600/30 transition-all duration-150 hover:from-violet-500 hover:to-fuchsia-400 hover:shadow-md hover:shadow-violet-600/40"
        >
          Sign in with Google
        </a>
      </div>
    </div>
  );
}
