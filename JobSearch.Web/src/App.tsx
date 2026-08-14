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
import { useMe, useLogout } from "./hooks/useAuth";

const NAV_LINKS = [
  { to: "/",             label: "Dashboard"    },
  { to: "/generate",     label: "Generate"     },
  { to: "/discover",     label: "Discover"     },
  { to: "/applications", label: "Applications" },
  { to: "/activity",     label: "Activity"     },
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

function OnboardingRedirect() {
  const location = useLocation();
  if (ONBOARDING_ROUTES.includes(location.pathname)) return null;
  return <Navigate to="/profile" replace />;
}

export default function App() {
  const { data: me, loading } = useMe();
  const { execute: doLogout } = useLogout();

  async function handleLogout() {
    await doLogout();
    window.location.href = "/";
  }

  if (loading) return null;
  if (!me) return <LandingPage />;

  return (
    <BrowserRouter>
      <div className="min-h-screen bg-gray-50">
        <header className="border-b border-gray-200 bg-white shadow-sm">
          <div className="mx-auto flex max-w-7xl items-center justify-between px-6 py-4">
            <h1 className="text-lg font-semibold text-gray-800">Job Search</h1>
            <nav className="flex items-center gap-1">
              {NAV_LINKS.map(({ to, label }) => (
                <NavLink
                  key={to}
                  to={to}
                  end={to === "/"}
                  className={({ isActive }) =>
                    `rounded-lg px-3 py-1.5 text-sm font-medium transition-colors ${
                      isActive
                        ? "bg-blue-50 text-blue-700"
                        : "text-gray-500 hover:bg-gray-100 hover:text-gray-700"
                    }`
                  }
                >
                  {label}
                </NavLink>
              ))}
              <div className="ml-4 flex items-center gap-3 border-l border-gray-200 pl-4">
                <span className="text-xs text-gray-400">{me.email}</span>
                <button
                  onClick={handleLogout}
                  className="rounded-lg px-3 py-1.5 text-sm font-medium text-gray-500 transition-colors hover:bg-gray-100 hover:text-gray-700"
                >
                  Sign out
                </button>
              </div>
            </nav>
          </div>
        </header>

        <main className="mx-auto max-w-7xl px-6 py-8">
          {me.needsOnboarding && <OnboardingRedirect />}
          <Routes>
            <Route path="/"             element={<DashboardPage />} />
            <Route path="/generate"     element={<GeneratePage />} />
            <Route path="/discover"     element={<DiscoveriesPage />} />
            <Route path="/applications" element={<ApplicationsPage />} />
            <Route path="/activity"     element={<ActivityPage />} />
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
