import { BrowserRouter, Routes, Route, NavLink } from "react-router-dom";
import { DashboardPage } from "./pages/DashboardPage";
import { ApplicationsPage } from "./pages/ApplicationsPage";
import { ActivityPage } from "./pages/ActivityPage";
import { HealthPage } from "./pages/HealthPage";

const NAV_LINKS = [
  { to: "/",            label: "Dashboard"    },
  { to: "/applications", label: "Applications" },
  { to: "/activity",    label: "Activity"     },
  { to: "/health",      label: "Health"       },
];

export default function App() {
  return (
    <BrowserRouter>
      <div className="min-h-screen bg-gray-50">
        <header className="border-b border-gray-200 bg-white shadow-sm">
          <div className="mx-auto flex max-w-7xl items-center justify-between px-6 py-4">
            <h1 className="text-lg font-semibold text-gray-800">Job Search</h1>
            <nav className="flex gap-1">
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
            </nav>
          </div>
        </header>

        <main className="mx-auto max-w-7xl px-6 py-8">
          <Routes>
            <Route path="/"             element={<DashboardPage />} />
            <Route path="/applications" element={<ApplicationsPage />} />
            <Route path="/activity"     element={<ActivityPage />} />
            <Route path="/health"       element={<HealthPage />} />
          </Routes>
        </main>
      </div>
    </BrowserRouter>
  );
}
