import { useEffect, useState } from "react";
import { BrowserRouter, Routes, Route, NavLink } from "react-router-dom";
import { DashboardPage } from "./pages/DashboardPage";
import { ApplicationsPage } from "./pages/ApplicationsPage";
import { ActivityPage } from "./pages/ActivityPage";
import { HealthPage } from "./pages/HealthPage";
import { DiscoveriesPage } from "./pages/DiscoveriesPage";
import { fetchMe, logout } from "./api";

const NAV_LINKS = [
  { to: "/",             label: "Dashboard"    },
  { to: "/discover",     label: "Discover"     },
  { to: "/applications", label: "Applications" },
  { to: "/activity",     label: "Activity"     },
  { to: "/health",       label: "Health"       },
];

type AuthState = "loading" | "authenticated" | "unauthenticated";

export default function App() {
  const [auth, setAuth] = useState<AuthState>("loading");
  const [email, setEmail] = useState<string | null>(null);

  useEffect(() => {
    fetchMe()
      .then((me) => {
        setEmail(me.email);
        setAuth("authenticated");
      })
      .catch(() => setAuth("unauthenticated"));
  }, []);

  useEffect(() => {
    if (auth === "unauthenticated") {
      window.location.href = "/api/v1/auth/login";
    }
  }, [auth]);

  async function handleLogout() {
    await logout();
    window.location.href = "/api/v1/auth/login";
  }

  if (auth !== "authenticated") return null;

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
                <span className="text-xs text-gray-400">{email}</span>
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
          <Routes>
            <Route path="/"             element={<DashboardPage />} />
            <Route path="/discover"     element={<DiscoveriesPage />} />
            <Route path="/applications" element={<ApplicationsPage />} />
            <Route path="/activity"     element={<ActivityPage />} />
            <Route path="/health"       element={<HealthPage />} />
          </Routes>
        </main>
      </div>
    </BrowserRouter>
  );
}
