import { useEffect, useState } from "react";
import { fetchProfile, updateProfile } from "../api";

const TEXTAREA = "w-full rounded-lg border border-gray-200 p-3 font-mono text-xs text-gray-700 focus:outline-none focus:ring-2 focus:ring-blue-300";
const LABEL = "mb-2 block text-sm font-medium text-gray-700";

export function SettingsPage() {
  const [background, setBackground] = useState("");
  const [cvBase, setCvBase] = useState("");
  const [jobCriteria, setJobCriteria] = useState("");
  const [updatedAt, setUpdatedAt] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetchProfile()
      .then(p => {
        setBackground(p.background);
        setCvBase(p.cvBase);
        setJobCriteria(p.jobCriteria);
        setUpdatedAt(p.updatedAt);
      })
      .catch(e => setError(e instanceof Error ? e.message : "Failed to load profile"))
      .finally(() => setLoading(false));
  }, []);

  async function handleSave() {
    setSaving(true);
    setError(null);
    try {
      const updated = await updateProfile({ background, cvBase, jobCriteria });
      setUpdatedAt(updated.updatedAt);
      setSaved(true);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to save");
    } finally {
      setSaving(false);
    }
  }

  if (loading) return <div className="py-12 text-center text-sm text-gray-400">Loading…</div>;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold text-gray-700">Settings</h2>
        {updatedAt && (
          <span className="text-xs text-gray-400">
            Last updated {new Date(updatedAt).toLocaleString("en-AU")}
          </span>
        )}
      </div>
      <p className="text-sm text-gray-500">
        Edit your background, base CV, and job criteria directly. Changes apply to the next
        CV, cover letter, answer, or posting evaluation you request — nothing here needs a
        separate re-run.
      </p>

      <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
        <label className={LABEL}>Background</label>
        <textarea value={background} onChange={e => { setBackground(e.target.value); setSaved(false); }} rows={16} className={TEXTAREA} />
      </div>

      <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
        <label className={LABEL}>Base CV</label>
        <textarea value={cvBase} onChange={e => { setCvBase(e.target.value); setSaved(false); }} rows={16} className={TEXTAREA} />
      </div>

      <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
        <label className={LABEL}>Job criteria</label>
        <textarea value={jobCriteria} onChange={e => { setJobCriteria(e.target.value); setSaved(false); }} rows={16} className={TEXTAREA} />
      </div>

      <div className="flex items-center gap-3">
        <button
          onClick={handleSave}
          disabled={saving}
          className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:bg-gray-300"
        >
          {saving ? "Saving…" : "Save changes"}
        </button>
        {saved && <span className="text-sm text-emerald-600">Saved.</span>}
      </div>

      {error && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">{error}</div>
      )}
    </div>
  );
}
