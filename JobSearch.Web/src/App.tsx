import { BrowserRouter, Routes, Route, Navigate, useLocation } from "react-router-dom";
import { AuthPage } from "./pages/AuthPage";
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
import { SettingsShell } from "./components/SettingsShell";
import {
  AppShell,
  TopNav,
  NavItem,
  BottomTabs,
  Tab,
  AccountMenu,
  CreditPill,
  ThemeToggle,
  ActivityIcon,
  SearchIcon,
  DocumentIcon,
  ChecklistIcon,
  SlidersIcon,
  SettingsIcon,
  HelpIcon,
  LifebuoyIcon,
  SignOutIcon,
  type AccountMenuItem,
} from "./ui";
import { useMe, useLogout } from "./hooks/useAuth";
import { useSiteStatus } from "./hooks/useSiteStatus";
import { MeProvider } from "./hooks/useMeContext";
import { MaintenanceNotice } from "./components/MaintenanceNotice";
import { AnnouncementBanner } from "./components/AnnouncementBanner";

// Daily-work destinations: the top nav on desktop, the bottom tab bar on mobile — both capped at
// four by the design (see ui/Nav.tsx's own note on why). Discover/Applications are Tier 2-only,
// the same gate the backend enforces on those endpoints (RequireTier2Async).
const PRIMARY_LINKS = [
  { to: "/",             label: "Today",        Icon: ActivityIcon                  },
  { to: "/discover",     label: "Discover",     Icon: SearchIcon,    tier2Only: true },
  { to: "/generate",     label: "Generate",     Icon: DocumentIcon                  },
  { to: "/applications", label: "Applications", Icon: ChecklistIcon, tier2Only: true },
];

// Setup rather than daily work, so it lives in the account menu instead — see
// ui/AccountMenu.tsx's own doc comment, which names this exact set.
const ACCOUNT_LINKS = [
  { to: "/resume-builder", label: "Resume",   Icon: DocumentIcon                  },
  { to: "/criteria",       label: "Criteria", Icon: SlidersIcon                   },
  { to: "/sources",        label: "Sources",  Icon: SearchIcon,   tier2Only: true },
  { to: "/settings",       label: "Settings", Icon: SettingsIcon                  },
  { to: "/help",           label: "Help",     Icon: HelpIcon                      },
  { to: "/support",        label: "Support",  Icon: LifebuoyIcon                  },
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

function isActive(pathname: string, to: string): boolean {
  return to === "/" ? pathname === "/" : pathname.startsWith(to);
}

type Me = NonNullable<ReturnType<typeof useMe>["data"]>;

export default function App() {
  const { data: me, loading, reload } = useMe();
  // Independent of useMe() entirely — both a logged-in and a logged-out visitor need to see
  // maintenance mode/the banner, so this can't be derived from or gated on auth state.
  const { data: siteStatus, loading: siteStatusLoading } = useSiteStatus();

  // Only blank the screen on the very first load, when there's nothing to show yet. A later
  // reloadMe() (after a credit-spending action) also flips `loading`, but `me` is still present —
  // rendering through it keeps the app mounted instead of flashing blank and remounting every page.
  // Same reasoning for siteStatusLoading: block only until the very first status check lands, so
  // the app doesn't flash its normal content for a moment before the maintenance notice appears.
  if (loading && !me) return null;
  if (siteStatusLoading && !siteStatus) return null;

  // Maintenance mode short-circuits the entire app — before BrowserRouter/AuthedApp/
  // LoggedOutRoutes mount, regardless of session state. See MaintenanceNotice's own comment.
  if (siteStatus?.maintenanceMode) {
    return <MaintenanceNotice message={siteStatus.maintenanceMessage} />;
  }

  // BrowserRouter now wraps both states: the logged-out marketing/auth routes and the
  // authenticated app. It used to mount only once a session existed, which is why the old
  // landing page had to read its own query params off window.location by hand — there was no
  // router to give /signin, /register or the reset link real routes.
  //
  // MeProvider exposes `reload` to any credit-spending descendant so the header's credit pill can
  // refresh the moment a generation/revision resolves, instead of going stale until a full reload.
  return (
    <BrowserRouter>
      {/* The banner sits above whichever branch renders below — the app stays fully usable
          underneath it, unlike maintenance mode's full takeover above. */}
      {siteStatus?.bannerActive && siteStatus.bannerMessage && (
        <AnnouncementBanner message={siteStatus.bannerMessage} />
      )}
      {me ? (
        <MeProvider me={me} reloadMe={reload}>
          <AuthedApp me={me} />
        </MeProvider>
      ) : (
        <LoggedOutRoutes />
      )}
    </BrowserRouter>
  );
}

// The logged-out surface: the marketing landing page at /, and the auth screens at /signin and
// /register. The API redirects a failed Google/activation attempt and the emailed reset link back
// to / with a query param (see Program.cs) rather than to a dedicated path, so / reads those and
// hands off to the auth screen when either is present.
function LoggedOutRoutes() {
  const params = new URLSearchParams(useLocation().search);
  const resetToken = params.get("resetToken");
  const authError = params.get("authError");

  return (
    <Routes>
      <Route path="/signin" element={<AuthPage initialTab="signin" />} />
      <Route path="/register" element={<AuthPage initialTab="register" />} />
      <Route
        path="/"
        element={
          resetToken || authError ? (
            <AuthPage initialTab="signin" resetToken={resetToken} authError={authError} />
          ) : (
            <LandingPage />
          )
        }
      />
      {/* Any other path while logged out lands on the marketing page rather than a blank router. */}
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}

function AuthedApp({ me }: { me: Me }) {
  // Hidden for the full forced-step onboarding flow (CV -> Criteria -> Sources), not just the
  // very first page — a nav bar full of links to pages the user hasn't set up yet (and can't
  // usefully visit, since StepRedirect just bounces them back) is noise during a guided,
  // linear first-run experience. It reappears on its own once every step is done, since these
  // flags come from a fresh /auth/me read after each step's save.
  const isOnboarding = me.needsOnboarding || me.needsCriteria || me.needsSourceSelection;

  return isOnboarding ? (
    <div className="min-h-screen bg-bg text-ink">
      <main className="px-4 py-8 sm:px-6">
        <PageBody me={me} />
      </main>
    </div>
  ) : (
    <AuthedShell me={me} />
  );
}

// Split out from App so useLocation (for nav active-state) and useLogout only run once a session
// exists and the full chrome is actually showing.
function AuthedShell({ me }: { me: Me }) {
  const { execute: doLogout } = useLogout();
  const location = useLocation();
  const isTier2 = me.tier === "Tier2";

  async function handleLogout() {
    await doLogout();
    window.location.href = "/";
  }

  const primaryLinks = PRIMARY_LINKS.filter(l => !l.tier2Only || isTier2);
  const accountLinks = ACCOUNT_LINKS.filter(l => !l.tier2Only || isTier2);

  const accountMenuItems: AccountMenuItem[] = [
    ...accountLinks.map(({ to, label, Icon }) => ({ label, href: to, icon: <Icon /> })),
    { label: "Sign out", onSelect: handleLogout, icon: <SignOutIcon />, separated: true },
  ];

  // firstName is null until a real CV/Background exists (still possible here, briefly, between
  // onboarding finishing and the next /auth/me read) — the email is always present, so it is
  // always a usable name for the avatar's initials even before that.
  const accountName = me.firstName ?? me.email;

  return (
    <AppShell
      // Fills the viewport rather than a centered column — see the earlier full-desktop-width
      // fix this preserves. AppShell defaults to a max-w-7xl centered frame (see its own note),
      // which is the right call for a component gallery but not for this app.
      contentClassName="max-w-none"
      nav={
        <TopNav>
          {primaryLinks.map(({ to, label, Icon }) => (
            <NavItem key={to} href={to} active={isActive(location.pathname, to)}>
              <span className="inline-flex items-center gap-1.5">
                <Icon className="h-3.5 w-3.5" />
                {label}
              </span>
            </NavItem>
          ))}
        </TopNav>
      }
      actions={
        <>
          <CreditPill credits={me.creditBalance} compact className="sm:hidden" />
          <CreditPill credits={me.creditBalance} className="max-sm:hidden" />
          <ThemeToggle />
          <AccountMenu name={accountName} email={me.firstName ? me.email : undefined} items={accountMenuItems} />
        </>
      }
      tabs={
        <BottomTabs>
          {primaryLinks.map(({ to, label, Icon }) => (
            <Tab key={to} href={to} active={isActive(location.pathname, to)} icon={<Icon />} label={label} />
          ))}
        </BottomTabs>
      }
    >
      <PageBody me={me} />
    </AppShell>
  );
}

// Split out so `key={location.pathname}` can retrigger the fade-in-up entrance animation on
// every route change — a plain CSS keyframe (see index.css), not a transition library, so it
// works identically everywhere and costs nothing when JS is otherwise idle.
function PageBody({ me }: { me: Me }) {
  const location = useLocation();

  return (
    <>
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
          <Route
            path="/sources"
            element={
              <SettingsShell activeKey="sources" title="Choose your sources" tagline="Tell us where to look, and how you want applications tracked.">
                <SourcesPage hideHeader />
              </SettingsShell>
            }
          />
          {/* A user who already has a real background (needsOnboarding false — see the flag's
              definition in Program.cs) shouldn't see the blank build-from-scratch/upload intake
              here; that only makes sense for a genuinely new user. Settings is the actual
              "edit your existing info" page for everyone else. */}
          <Route path="/profile"            element={me.needsOnboarding ? <ResumeIntakePage /> : <Navigate to="/settings" replace />} />
          <Route path="/resume-builder"     element={<ResumeBuilderPage />} />
          <Route
            path="/criteria"
            element={
              <SettingsShell
                activeKey="criteria"
                title="Job criteria"
                tagline="What you're actually looking for, precise enough to tell a good match from a bad one."
              >
                <JobCriteriaPage hideHeader />
              </SettingsShell>
            }
          />
          <Route path="/settings"           element={<SettingsPage />} />
          <Route
            path="/help"
            element={
              <SettingsShell
                activeKey="help"
                title="How it works"
                tagline="Short answers to what people ask most. If yours is not here, the support form goes straight to a real inbox."
              >
                <HelpPage hideHeader />
              </SettingsShell>
            }
          />
          <Route path="/support"            element={<SupportPage />} />
          <Route path="/onboarding/cv"       element={<OnboardingCvPage tier={me.tier} />} />
          <Route path="/onboarding/criteria" element={<OnboardingCriteriaPage tier={me.tier} />} />
          <Route path="/onboarding/sources"  element={<OnboardingSourcesPage tier={me.tier} />} />
          {/* Logged-out-only paths (/signin, /register) and anything else unknown land here
              instead of a blank <main> — LoggedOutRoutes already has this same catch-all. */}
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </div>
    </>
  );
}
