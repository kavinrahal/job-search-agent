import { useState, useEffect, useCallback } from "react";
import { fetchSummary } from "./api";
import type { Summary } from "./types";
import { SummaryCards } from "./components/SummaryCards";
import { EmailTable } from "./components/EmailTable";

export default function App() {
  const [summary, setSummary] = useState<Summary | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);
  const [refreshing, setRefreshing] = useState(false);

  const loadSummary = useCallback(async () => {
    try {
      setSummary(await fetchSummary());
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load summary");
    }
  }, []);

  useEffect(() => { void loadSummary(); }, [loadSummary, refreshKey]);

  async function handleRefresh() {
    setRefreshing(true);
    await loadSummary();
    setRefreshKey(k => k + 1);
    setRefreshing(false);
  }

  return (
    <div className="min-h-screen bg-gray-50">
      <header className="flex items-center justify-between border-b border-gray-200 bg-white px-6 py-4 shadow-sm">
        <h1 className="text-xl font-semibold text-gray-800">Job Search Dashboard</h1>
        <button
          onClick={handleRefresh}
          disabled={refreshing}
          className="rounded-lg border border-gray-200 px-3 py-1.5 text-sm text-gray-500 hover:bg-gray-50 disabled:opacity-40"
        >
          {refreshing ? "Refreshing…" : "Refresh"}
        </button>
      </header>

      <main className="mx-auto max-w-7xl space-y-8 px-6 py-8">
        {error && (
          <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
            {error} — make sure the API server is running on port 5000.
          </div>
        )}

        {summary && (
          <section>
            <h2 className="mb-4 text-lg font-semibold text-gray-700">Overview</h2>
            <SummaryCards summary={summary} />
          </section>
        )}

        <section>
          <h2 className="mb-4 text-lg font-semibold text-gray-700">Emails</h2>
          <EmailTable refreshKey={refreshKey} />
        </section>
      </main>
    </div>
  );
}
