import { useState, useEffect, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import { fetchSummary } from "../api";
import type { Summary } from "../types";
import { SummaryCards } from "../components/SummaryCards";
import { EmailTable } from "../components/EmailTable";

export function DashboardPage() {
  const navigate = useNavigate();
  const [summary, setSummary] = useState<Summary | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);
  const [refreshing, setRefreshing] = useState(false);

  // Owned here so SummaryCards can drive them and EmailTable stays in sync.
  const [category, setCategory] = useState("");
  const [jobRelatedOnly, setJobRelatedOnly] = useState(false);

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
    <div className="space-y-8">
      {error && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          {error}
        </div>
      )}

      <section>
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-semibold text-gray-700">Overview</h2>
          <button
            onClick={handleRefresh}
            disabled={refreshing}
            className="rounded-lg border border-gray-200 px-3 py-1.5 text-sm text-gray-500 hover:bg-gray-50 disabled:opacity-40"
          >
            {refreshing ? "Refreshing…" : "Refresh"}
          </button>
        </div>
        {summary && (
          <SummaryCards
            summary={summary}
            onTotalClick={() => { setCategory(""); setJobRelatedOnly(false); }}
            onJobRelatedClick={() => { setJobRelatedOnly(true); setCategory(""); }}
            onApplicationsClick={() => navigate("/applications")}
            onCategoryClick={cat => { setCategory(cat); setJobRelatedOnly(false); }}
            onStatusClick={status => navigate(`/applications?status=${status}`)}
          />
        )}
      </section>

      <section>
        <h2 className="mb-4 text-lg font-semibold text-gray-700">Emails</h2>
        <EmailTable
          refreshKey={refreshKey}
          category={category}
          jobRelatedOnly={jobRelatedOnly}
          onCategoryChange={setCategory}
          onJobRelatedChange={setJobRelatedOnly}
        />
      </section>
    </div>
  );
}
