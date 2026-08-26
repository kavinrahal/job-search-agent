import { useState } from "react";
import { BrowserRouter, Routes, Route, Link, NavLink, Navigate, useLocation } from "react-router-dom";
import { DashboardPage } from "./pages/DashboardPage";
import { ApplicationsPage } from "./pages/ApplicationsPage";
import { DiscoveriesPage } from "./pages/DiscoveriesPage";
import { ResumeIntakePage } from "./pages/ResumeIntakePage";
import { ResumeBuilderPage } from "./pages/ResumeBuilderPage";
import { JobCriteriaPage } from "./pages/JobCriteriaPage";
import { SettingsPage } from "./pages/SettingsPage";
import { LandingPage } from "./pages/LandingPage";
import { GeneratePage } from "./pages/GeneratePage";
import { SupportPage } from "./pages/SupportPage";
import { HelpPage } from "./pages/HelpPage";
import { SourcesPage } from "./pages/SourcesPage";
import { OnboardingCvPage } from "./pages/onboarding/OnboardingCvPage";
import { OnboardingCriteriaPage } from "./pages/onboarding/OnboardingCriteriaPage";
import { OnboardingSourcesPage } from "./pages/onboarding/OnboardingSourcesPage";
import { ThemeToggle } from "./components/ThemeToggle";
import { useMe, useLogout } from "./hooks/useAuth";

// Discover/Applications/Sources are Tier 2-exclusive (see the backend's matching
// RequireTier2Async gate on those endpoints). Activity and Health used to be separate
// nav items too — both are now sections on the Tier 2 dashboard instead, not standalone
// pages, so there's nothing left to link to here.
const NAV_LINKS = [
  { to: "/",             label: "Dashboard"    },
  { to: "/generate",     label: "Generate"     },
  { to: "/discover",     label: "Discover",     tier2Only: true },
  { to: "/applications", label: "Applications", tier2Only: true },
  { to: "/sources",      label: "Sources",      tier2Only: true },
  { to: "/profile",      label: "Profile"      },
  { to: "/resume-builder", label: "Resume Builder" },
  { to: "/criteria",     label: "Criteria"     },
  { to: "/settings",     label: "Settings"     },
  { to: "/help",         label: "Help"         },
  { to: "/support",      label: "Support"      },
];

// Routes a brand new user (blank Background, per /auth/me's needsOnboarding flag) can visit
// without being bounced back to the resume intake step — the dedicated onboarding step, plus
// Settings as an escape hatch so they're never fully trapped on one page.
const ONBOARDING_ROUTES = ["/onboarding/cv", "/settings"];

// Next step: Background is saved but Job Criteria has never been visited/saved
// (needsCriteria). Same escape hatch to Settings as every other step below, plus
// /resume-builder — the build-from-scratch onboarding detour (see ResumeIntakePage.handleSave)
// lands here in exactly this needsCriteria-true window, so without it StepRedirect would bounce
// the user off Resume Builder before they can interact with it.
const CRITERIA_ROUTES = ["/onboarding/criteria", "/resume-builder", "/settings"];

// Same idea, one step later: a Tier 2 user who hasn't picked sources yet (needsSourceSelection)
// can still reach Settings to back out, but everything else bounces to /sources first.
const SOURCES_ROUTES = ["/onboarding/sources", "/settings"];

function StepRedirect({ allowedRoutes, to }: { allowedRoutes: string[]; to: string }) {
  const location = useLocation();
  if (allowedRoutes.includes(location.pathname)) return null;
  return <Navigate to={to} replace />;
}

function NavLinks({ links, onNavigate, className }: {
  links: typeof NAV_LINKS; onNavigate?: () => void; className: (isActive: boolean) => string;
}) {
  return (
    <>
      {links.map(({ to, label }) => (
        <NavLink
          key={to}
          to={to}
          end={to === "/"}
          onClick={onNavigate}
          className={({ isActive }) => className(isActive)}
        >
          {label}
        </NavLink>
      ))}
    </>
  );
}

function AccountMenu({ email, onLogout, className }: { email: string; onLogout: () => void; className: string }) {
  return (
    <div className={className}>
      <span className="text-xs text-gray-400 dark:text-gray-500">{email}</span>
      <button
        onClick={onLogout}
        className="rounded-lg px-3 py-1.5 text-sm font-medium text-gray-500 transition-colors hover:bg-gray-100 hover:text-gray-700 dark:text-gray-400 dark:hover:bg-gray-800 dark:hover:text-gray-200"
      >
        Sign out
      </button>
    </div>
  );
}

// Small gradient mark next to the wordmark — the bold/vibrant accent the rest of the app's
// buttons and highlights echo, so the brand itself sets the palette rather than tacking
// color on afterward.
function Logo() {
  return (
    <Link to="/" className="flex items-center gap-2">
      <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-gradient-to-br from-violet-600 to-fuchsia-500 text-white shadow-sm shadow-violet-600/30">
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2.25} className="h-4.5 w-4.5">
          <path strokeLinecap="round" strokeLinejoin="round" d="M13 2 3 14h7l-1 8 10-12h-7l1-8z" />
        </svg>
      </div>
      <h1 className="text-lg font-semibold tracking-tight text-gray-900 dark:text-white">Work Santa</h1>
    </Link>
  );
}

const NAV_LINK_CLASS = (isActive: boolean) =>
  `rounded-lg px-3 py-1.5 text-sm font-medium transition-colors duration-150 ${
    isActive
      ? "bg-violet-50 text-violet-700 dark:bg-violet-500/15 dark:text-violet-300"
      : "text-gray-600 hover:bg-gray-100 hover:text-gray-900 dark:text-gray-400 dark:hover:bg-gray-800 dark:hover:text-gray-100"
  }`;

const MOBILE_NAV_LINK_CLASS = (isActive: boolean) =>
  `rounded-lg px-3 py-2 text-sm font-medium transition-colors duration-150 ${
    isActive
      ? "bg-violet-50 text-violet-700 dark:bg-violet-500/15 dark:text-violet-300"
      : "text-gray-600 hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-800"
  }`;

export default function App() {
  const { data: me, loading } = useMe();
  const { execute: doLogout } = useLogout();
  const [menuOpen, setMenuOpen] = useState(false);

  async function handleLogout() {
    await doLogout();
    window.location.href = "/";
  }

  if (loading) return null;
  if (!me) return <LandingPage />;

  const navLinks = NAV_LINKS.filter(l => !l.tier2Only || me.tier === "Tier2");
  // Hidden for the full forced-step onboarding flow (CV -> Criteria -> Sources), not just the
  // very first page — a nav bar full of links to pages the user hasn't set up yet (and can't
  // usefully visit, since StepRedirect just bounces them back) is noise during a guided,
  // linear first-run experience. It reappears on its own once every step is done, since these
  // flags come from a fresh /auth/me read after each step's save.
  const isOnboarding = me.needsOnboarding || me.needsCriteria || me.needsSourceSelection;

  return (
    <BrowserRouter>
      <div className="min-h-screen bg-gray-50 dark:bg-gray-950">
        {!isOnboarding && (
          <header className="sticky top-0 z-20 border-b border-gray-200/80 bg-white/80 backdrop-blur-md dark:border-gray-800/80 dark:bg-gray-950/80">
            <div className="mx-auto flex max-w-7xl items-center justify-between px-4 py-4 sm:px-6">
              <Logo />

              <nav className="hidden items-center gap-1 md:flex">
                <NavLinks links={navLinks} className={NAV_LINK_CLASS} />
                <ThemeToggle className="ml-2" />
                <AccountMenu
                  email={me.email}
                  onLogout={handleLogout}
                  className="ml-2 flex items-center gap-3 border-l border-gray-200 pl-4 dark:border-gray-800"
                />
              </nav>

              <div className="flex items-center gap-1 md:hidden">
                <ThemeToggle />
                <button
                  onClick={() => setMenuOpen(o => !o)}
                  aria-label={menuOpen ? "Close menu" : "Open menu"}
                  className="rounded-lg p-2 text-gray-500 transition-colors hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-800"
                >
                  {menuOpen ? (
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-6 w-6">
                      <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
                    </svg>
                  ) : (
                    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-6 w-6">
                      <path strokeLinecap="round" strokeLinejoin="round" d="M4 6h16M4 12h16M4 18h16" />
                    </svg>
                  )}
                </button>
              </div>
            </div>

            {menuOpen && (
              <nav className="flex flex-col gap-1 border-t border-gray-100 px-4 py-3 dark:border-gray-800 md:hidden">
                <NavLinks links={navLinks} onNavigate={() => setMenuOpen(false)} className={MOBILE_NAV_LINK_CLASS} />
                <AccountMenu
                  email={me.email}
                  onLogout={handleLogout}
                  className="mt-2 flex items-center justify-between border-t border-gray-100 pt-3 dark:border-gray-800"
                />
              </nav>
            )}
          </header>
        )}

        <PageBody me={me} />
      </div>
    </BrowserRouter>
  );
}

// Split out so `key={location.pathname}` can retrigger the fade-in-up entrance animation on
// every route change — a plain CSS keyframe (see index.css), not a transition library, so it
// works identically everywhere and costs nothing when JS is otherwise idle.
function PageBody({ me }: { me: NonNullable<ReturnType<typeof useMe>["data"]> }) {
  const location = useLocation();

  return (
    <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6">
      {me.needsOnboarding && <StepRedirect allowedRoutes={ONBOARDING_ROUTES} to="/onboarding/cv" />}
      {!me.needsOnboarding && me.needsCriteria && <StepRedirect allowedRoutes={CRITERIA_ROUTES} to="/onboarding/criteria" />}
      {!me.needsOnboarding && !me.needsCriteria && me.needsSourceSelection && <StepRedirect allowedRoutes={SOURCES_ROUTES} to="/onboarding/sources" />}
      {/* The reverse guard: once every step is done, the /onboarding/* routes have no reason
          to exist anymore — without this, typing one back into the URL bar would still render
          the wizard for a fully set-up user. */}
      {!me.needsOnboarding && !me.needsCriteria && !me.needsSourceSelection && location.pathname.startsWith("/onboarding/") && (
        <Navigate to="/" replace />
      )}
      <div key={location.pathname} className="animate-fade-in-up">
        <Routes>
          <Route path="/"                   element={<DashboardPage />} />
          <Route path="/generate"           element={<GeneratePage />} />
          <Route path="/discover"           element={<DiscoveriesPage />} />
          <Route path="/applications"       element={<ApplicationsPage />} />
          <Route path="/sources"            element={<SourcesPage />} />
          {/* A user who already has a real background (needsOnboarding false — see the flag's
              definition in Program.cs) shouldn't see the blank build-from-scratch/upload intake
              here; that only makes sense for a genuinely new user. Settings is the actual
              "edit your existing info" page for everyone else. */}
          <Route path="/profile"            element={me.needsOnboarding ? <ResumeIntakePage /> : <Navigate to="/settings" replace />} />
          <Route path="/resume-builder"     element={<ResumeBuilderPage />} />
          <Route path="/criteria"           element={<JobCriteriaPage />} />
          <Route path="/settings"           element={<SettingsPage />} />
          <Route path="/help"               element={<HelpPage />} />
          <Route path="/support"            element={<SupportPage />} />
          <Route path="/onboarding/cv"       element={<OnboardingCvPage tier={me.tier} />} />
          <Route path="/onboarding/criteria" element={<OnboardingCriteriaPage tier={me.tier} />} />
          <Route path="/onboarding/sources"  element={<OnboardingSourcesPage tier={me.tier} />} />
        </Routes>
      </div>
    </main>
  );
}
