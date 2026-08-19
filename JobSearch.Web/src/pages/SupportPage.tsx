import { useState } from "react";
import { useSupportMessage } from "../hooks/useSupport";
import { PageTagline } from "../components/PageTagline";
import { CARD, PRIMARY_BUTTON } from "../lib/styles";

export function SupportPage() {
  const [message, setMessage] = useState("");
  const [sent, setSent] = useState(false);
  const { execute, loading, error } = useSupportMessage();

  async function handleSubmit() {
    await execute(message);
    setMessage("");
    setSent(true);
  }

  return (
    <div className="space-y-6">
      <h2 className="text-lg font-semibold text-gray-700 dark:text-gray-200">Support</h2>
      <PageTagline>Something broken, confusing, or just want to say hi? We're listening.</PageTagline>

      <div className={CARD}>
        <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-gray-200">
          Describe the issue or question
        </label>
        <textarea
          value={message}
          onChange={e => { setMessage(e.target.value); setSent(false); }}
          rows={6}
          className="w-full rounded-lg border border-gray-200 bg-white p-3 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-violet-400 dark:border-gray-700 dark:bg-gray-800 dark:text-gray-100 dark:focus:ring-violet-500"
        />
        <div className="mt-4 flex items-center gap-3">
          <button
            onClick={handleSubmit}
            disabled={message.trim().length === 0 || loading}
            className={PRIMARY_BUTTON}
          >
            {loading ? "Sending…" : "Send"}
          </button>
          {sent && <span className="text-sm text-emerald-600 dark:text-emerald-400">Sent. We'll get back to you.</span>}
        </div>
      </div>

      {error && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-950/30 dark:text-red-400">{error}</div>
      )}
    </div>
  );
}
