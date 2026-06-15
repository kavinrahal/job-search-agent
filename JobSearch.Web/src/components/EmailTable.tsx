import { useState, useEffect, useCallback } from "react";
import { fetchEmails } from "../api";
import type { EmailItem } from "../types";

const CATEGORY_LABELS: Record<string, string> = {
  application_confirmation: "Application confirmed",
  rejection: "Rejection",
  interview_invitation: "Interview invite",
  recruiter_outreach: "Recruiter outreach",
  scheduling_request: "Scheduling request",
  offer: "Offer",
  follow_up_needed: "Action needed",
  not_relevant: "Not relevant",
};

const CATEGORY_COLORS: Record<string, string> = {
  application_confirmation: "bg-green-100 text-green-700",
  rejection: "bg-red-100 text-red-700",
  interview_invitation: "bg-purple-100 text-purple-700",
  recruiter_outreach: "bg-blue-100 text-blue-700",
  scheduling_request: "bg-yellow-100 text-yellow-700",
  offer: "bg-emerald-100 text-emerald-700",
  follow_up_needed: "bg-orange-100 text-orange-700",
  not_relevant: "bg-gray-100 text-gray-500",
};

const PAGE_SIZE = 25;

export function EmailTable({ refreshKey = 0 }: { refreshKey?: number }) {
  const [emails, setEmails] = useState<EmailItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [category, setCategory] = useState("");
  const [jobRelatedOnly, setJobRelatedOnly] = useState(false);
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetchEmails({
        page,
        pageSize: PAGE_SIZE,
        category: category || undefined,
        jobRelatedOnly: jobRelatedOnly || undefined,
        from: from || undefined,
        to: to || undefined,
      });
      setEmails(res.items);
      setTotal(res.total);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Unknown error");
    } finally {
      setLoading(false);
    }
  }, [page, category, jobRelatedOnly, from, to]);

  useEffect(() => { void load(); }, [load, refreshKey]);

  const totalPages = Math.ceil(total / PAGE_SIZE);

  function resetFilters() {
    setCategory("");
    setJobRelatedOnly(false);
    setFrom("");
    setTo("");
    setPage(1);
  }

  return (
    <div className="space-y-4">
      {/* Filters */}
      <div className="flex flex-wrap items-end gap-3 rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
        <label className="flex flex-col gap-1 text-xs font-medium text-gray-500">
          Category
          <select
            value={category}
            onChange={e => { setCategory(e.target.value); setPage(1); }}
            className="rounded-lg border border-gray-200 px-2 py-1.5 text-sm text-gray-800 focus:outline-none focus:ring-2 focus:ring-blue-300"
          >
            <option value="">All</option>
            {Object.entries(CATEGORY_LABELS).map(([k, v]) => (
              <option key={k} value={k}>{v}</option>
            ))}
          </select>
        </label>

        <label className="flex flex-col gap-1 text-xs font-medium text-gray-500">
          From date
          <input
            type="date"
            value={from}
            onChange={e => { setFrom(e.target.value); setPage(1); }}
            className="rounded-lg border border-gray-200 px-2 py-1.5 text-sm text-gray-800 focus:outline-none focus:ring-2 focus:ring-blue-300"
          />
        </label>

        <label className="flex flex-col gap-1 text-xs font-medium text-gray-500">
          To date
          <input
            type="date"
            value={to}
            onChange={e => { setTo(e.target.value); setPage(1); }}
            className="rounded-lg border border-gray-200 px-2 py-1.5 text-sm text-gray-800 focus:outline-none focus:ring-2 focus:ring-blue-300"
          />
        </label>

        <label className="flex items-center gap-2 text-sm font-medium text-gray-600">
          <input
            type="checkbox"
            checked={jobRelatedOnly}
            onChange={e => { setJobRelatedOnly(e.target.checked); setPage(1); }}
            className="rounded"
          />
          Job-related only
        </label>

        <button
          onClick={resetFilters}
          className="ml-auto rounded-lg border border-gray-200 px-3 py-1.5 text-sm text-gray-500 hover:bg-gray-50"
        >
          Reset
        </button>
      </div>

      {/* Table */}
      <div className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
        {error && (
          <div className="p-4 text-sm text-red-600">{error}</div>
        )}
        {loading ? (
          <div className="p-8 text-center text-sm text-gray-400">Loading…</div>
        ) : emails.length === 0 ? (
          <div className="p-8 text-center text-sm text-gray-400">No emails found</div>
        ) : (
          <table className="w-full text-sm">
            <thead className="border-b border-gray-100 bg-gray-50 text-xs uppercase tracking-wide text-gray-400">
              <tr>
                <th className="px-4 py-3 text-left">Date</th>
                <th className="px-4 py-3 text-left">From</th>
                <th className="px-4 py-3 text-left">Subject</th>
                <th className="px-4 py-3 text-left">Company</th>
                <th className="px-4 py-3 text-left">Category</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {emails.map(email => (
                <tr key={email.messageId} className="hover:bg-gray-50">
                  <td className="whitespace-nowrap px-4 py-3 text-gray-400">
                    {new Date(email.receivedAt).toLocaleDateString("en-AU", {
                      day: "2-digit",
                      month: "short",
                      year: "numeric",
                    })}
                  </td>
                  <td className="max-w-[180px] truncate px-4 py-3 text-gray-600">
                    {email.from}
                  </td>
                  <td className="max-w-[300px] truncate px-4 py-3 font-medium text-gray-800">
                    {email.subject}
                    {email.roleTitle && (
                      <span className="ml-2 text-xs font-normal text-gray-400">
                        {email.roleTitle}
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-3 text-gray-500">{email.company ?? "—"}</td>
                  <td className="px-4 py-3">
                    {email.category ? (
                      <span
                        className={`rounded-full px-2 py-0.5 text-xs font-medium ${CATEGORY_COLORS[email.category] ?? "bg-gray-100 text-gray-600"}`}
                      >
                        {CATEGORY_LABELS[email.category] ?? email.category}
                      </span>
                    ) : (
                      <span className="text-gray-300">—</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-between text-sm text-gray-500">
          <span>
            {(page - 1) * PAGE_SIZE + 1}–{Math.min(page * PAGE_SIZE, total)} of {total}
          </span>
          <div className="flex gap-2">
            <button
              disabled={page === 1}
              onClick={() => setPage(p => p - 1)}
              className="rounded-lg border border-gray-200 px-3 py-1.5 disabled:opacity-40 hover:bg-gray-50"
            >
              ← Prev
            </button>
            <button
              disabled={page >= totalPages}
              onClick={() => setPage(p => p + 1)}
              className="rounded-lg border border-gray-200 px-3 py-1.5 disabled:opacity-40 hover:bg-gray-50"
            >
              Next →
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
