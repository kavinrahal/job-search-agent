import { useState } from "react";
import { submitSupportMessage } from "../api";

export function SupportPage() {
  const [message, setMessage] = useState("");
  const [sending, setSending] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit() {
    setSending(true);
    setError(null);
    try {
      await submitSupportMessage(message);
      setMessage("");
      setSent(true);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to send");
    } finally {
      setSending(false);
    }
  }

  return (
    <div className="space-y-6">
      <h2 className="text-lg font-semibold text-gray-700">Support</h2>

      <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
        <label className="mb-2 block text-sm font-medium text-gray-700">
          Describe the issue or question
        </label>
        <textarea
          value={message}
          onChange={e => { setMessage(e.target.value); setSent(false); }}
          rows={6}
          className="w-full rounded-lg border border-gray-200 p-3 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-blue-300"
        />
        <div className="mt-4 flex items-center gap-3">
          <button
            onClick={handleSubmit}
            disabled={message.trim().length === 0 || sending}
            className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:bg-gray-300"
          >
            {sending ? "Sending…" : "Send"}
          </button>
          {sent && <span className="text-sm text-emerald-600">Sent — we'll get back to you.</span>}
        </div>
      </div>

      {error && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">{error}</div>
      )}
    </div>
  );
}
