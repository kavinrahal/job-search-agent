import { useState } from "react";
import { BrowserRouter, Routes, Route, NavLink, Navigate, useLocation } from "react-router-dom";
import { DashboardPage } from "./pages/DashboardPage";
import { ApplicationsPage } from "./pages/ApplicationsPage";
import { ActivityPage } from "./pages/ActivityPage";
import { HealthPage } from "./pages/HealthPage";
import { DiscoveriesPage } from "./pages/DiscoveriesPage";
import { ResumeIntakePage } from "./pages/ResumeIntakePage";
import { JobCriteriaPage } from "./pages/JobCriteriaPage";
import { SettingsPage } from "./pages/SettingsPage";
import { LandingPage } from "./pages/LandingPage";
import { GeneratePage } from "./pages/GeneratePage";
import { SupportPage } from "./pages/SupportPage";
import { SourcesPage } from "./pages/SourcesPage";
import { useMe, useLogout } from "./hooks/useAuth";

const NAV_LINKS = [
  { to: "/",             label: "Dashboard"    },
  { to: "/generate",     label: "Generate"     },
  { to: "/discover",     label: "Discover"     },
  { to: "/applications", label: "Applications" },
  { to: "/activity",     label: "Activity"     },
  { to: "/sources",      label: "Sources",     tier2Only: true },
  { to: "/profile",      label: "Profile"      },
  { to: "/criteria",     label: "Criteria"     },
  { to: "/settings",     label: "Settings"     },
  { to: "/support",      label: "Support"      },
  { to: "/health",       label: "Health"       },
];

// Routes a brand new user (blank Background, per /auth/me's needsOnboarding flag) can visit
// without being bounced back to the resume intake step — lets them move on to job criteria
// or settings without a redirect loop, but still funnels them away from the empty Dashboard.
const ONBOARDING_ROUTES = ["/profile", "/criteria", "/settings"];

// Same idea, one step later: a Tier 2 user who hasn't picked sources yet (needsSourceSelection)
// can still reach Settings to back out, but everything else bounces to /sources first.
const SOURCES_ROUTES = ["/sources", "/settings"];

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
      <span className="text-xs text-gray-400">{email}</span>
      <button
        onClick={onLogout}
        className="rounded-lg px-3 py-1.5 text-sm font-medium text-gray-500 transition-colors hover:bg-gray-100 hover:text-gray-700"
      >
        Sign out
      </button>
    </div>
  );
}

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

  return (
    <BrowserRouter>
      <div className="min-h-screen bg-gray-50">
        <header className="border-b border-gray-200 bg-white shadow-sm">
          <div className="mx-auto flex max-w-7xl items-center justify-between px-4 py-4 sm:px-6">
            <h1 className="text-lg font-semibold text-gray-800">Job Search</h1>

            <nav className="hidden items-center gap-1 md:flex">
              <NavLinks
                links={navLinks}
                className={isActive =>
                  `rounded-lg px-3 py-1.5 text-sm font-medium transition-colors ${
                    isActive
                      ? "bg-blue-50 text-blue-700"
                      : "text-gray-500 hover:bg-gray-100 hover:text-gray-700"
                  }`
                }
              />
              <AccountMenu email={me.email} onLogout={handleLogout} className="ml-4 flex items-center gap-3 border-l border-gray-200 pl-4" />
            </nav>

            <button
              onClick={() => setMenuOpen(o => !o)}
              aria-label={menuOpen ? "Close menu" : "Open menu"}
              className="rounded-lg p-2 text-gray-500 hover:bg-gray-100 md:hidden"
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

          {menuOpen && (
            <nav className="flex flex-col gap-1 border-t border-gray-100 px-4 py-3 md:hidden">
              <NavLinks
                links={navLinks}
                onNavigate={() => setMenuOpen(false)}
                className={isActive =>
                  `rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
                    isActive
                      ? "bg-blue-50 text-blue-700"
                      : "text-gray-600 hover:bg-gray-100"
                  }`
                }
              />
              <AccountMenu email={me.email} onLogout={handleLogout} className="mt-2 flex items-center justify-between border-t border-gray-100 pt-3" />
            </nav>
          )}
        </header>

        <main className="mx-auto max-w-7xl px-4 py-8 sm:px-6">
          {me.needsOnboarding && <StepRedirect allowedRoutes={ONBOARDING_ROUTES} to="/profile" />}
          {!me.needsOnboarding && me.needsSourceSelection && <StepRedirect allowedRoutes={SOURCES_ROUTES} to="/sources" />}
          <Routes>
            <Route path="/"             element={<DashboardPage />} />
            <Route path="/generate"     element={<GeneratePage />} />
            <Route path="/discover"     element={<DiscoveriesPage />} />
            <Route path="/applications" element={<ApplicationsPage />} />
            <Route path="/activity"     element={<ActivityPage />} />
            <Route path="/sources"      element={<SourcesPage />} />
            <Route path="/profile"      element={<ResumeIntakePage />} />
            <Route path="/criteria"     element={<JobCriteriaPage />} />
            <Route path="/settings"     element={<SettingsPage />} />
            <Route path="/support"      element={<SupportPage />} />
            <Route path="/health"       element={<HealthPage />} />
          </Routes>
        </main>
      </div>
    </BrowserRouter>
  );
}
