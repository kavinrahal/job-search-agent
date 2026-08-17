import { useEffect, useState } from "react";
import { useProfile, useUpdateProfile } from "../hooks/useProfile";
import { JobCriteriaEditor } from "../components/JobCriteriaEditor";
import { parseJobCriteriaYaml, serializeJobCriteriaYaml, type JobCriteriaData } from "../lib/jobCriteriaYaml";

const EMPTY: JobCriteriaData = parseJobCriteriaYaml("");

export function JobCriteriaPage() {
  const { data: profile, loading: loadingProfile } = useProfile();
  const [criteria, setCriteria] = useState<JobCriteriaData>(EMPTY);
  const [saved, setSaved] = useState(false);
  const { execute, loading: saving, error } = useUpdateProfile();

  // Reflects whatever's already saved rather than always starting from defaults — editing
  // and saving from a blank slate would otherwise silently wipe out existing criteria.
  useEffect(() => {
    if (profile) setCriteria(parseJobCriteriaYaml(profile.jobCriteria));
  }, [profile]);

  async function handleSave() {
    await execute({ jobCriteria: serializeJobCriteriaYaml(criteria) });
    setSaved(true);
  }

  if (loadingProfile) return <div className="py-12 text-center text-sm text-gray-400">Loading…</div>;

  return (
    <div className="space-y-6">
      <h2 className="text-lg font-semibold text-gray-700">Job criteria</h2>

      <JobCriteriaEditor value={criteria} onChange={v => { setCriteria(v); setSaved(false); }} />

      <div className="flex items-center gap-3">
        <button
          onClick={handleSave}
          disabled={saving}
          className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:bg-gray-300"
        >
          {saving ? "Saving…" : "Save criteria"}
        </button>
        {saved && <span className="text-sm text-emerald-600">Saved.</span>}
      </div>

      {error && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">{error}</div>
      )}
    </div>
  );
}
