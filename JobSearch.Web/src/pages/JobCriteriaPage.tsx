import { useEffect, useState } from "react";
import { useProfile, useUpdateProfile } from "../hooks/useProfile";
import { useMe } from "../hooks/useAuth";
import { JobCriteriaEditor } from "../components/JobCriteriaEditor";
import { parseJobCriteriaYaml, serializeJobCriteriaYaml, type JobCriteriaData } from "../lib/jobCriteriaYaml";
import { PageTagline } from "../components/PageTagline";
import { PRIMARY_BUTTON } from "../lib/styles";

const EMPTY: JobCriteriaData = parseJobCriteriaYaml("");

export function JobCriteriaPage({ hideHeader = false, onSaved }: { hideHeader?: boolean; onSaved?: () => void } = {}) {
  const { data: profile, loading: loadingProfile } = useProfile();
  const { data: me } = useMe();
  const [criteria, setCriteria] = useState<JobCriteriaData>(EMPTY);
  const { execute, loading: saving, error } = useUpdateProfile();

  // Reflects whatever's already saved rather than always starting from defaults — editing
  // and saving from a blank slate would otherwise silently wipe out existing criteria.
  useEffect(() => {
    if (profile) setCriteria(parseJobCriteriaYaml(profile.jobCriteria));
  }, [profile]);

  // Full reload rather than re-setting local state — editing the Advanced (raw YAML) box
  // only updates the `extra` bucket locally, it doesn't re-derive the structured fields
  // from whatever was just saved. A reload re-fetches and re-parses the real saved value,
  // so the page always shows exactly what was persisted.
  async function handleSave() {
    await execute({ jobCriteria: serializeJobCriteriaYaml(criteria) });
    if (onSaved) onSaved();
    else window.location.reload();
  }

  if (loadingProfile) return <div className="py-12 text-center text-sm text-gray-400 dark:text-gray-500">Loading…</div>;

  return (
    <div className="space-y-6">
      {!hideHeader && (
        <>
          <h2 className="text-lg font-semibold text-gray-700 dark:text-gray-200">Job criteria</h2>
          <PageTagline>What you're actually looking for, precise enough to tell a good match from a bad one.</PageTagline>
        </>
      )}

      <JobCriteriaEditor value={criteria} onChange={setCriteria} tier={me?.tier ?? "Tier1"} />

      <div className="flex items-center gap-3">
        <button
          onClick={handleSave}
          disabled={saving}
          className={PRIMARY_BUTTON}
        >
          {saving ? "Saving…" : "Save criteria"}
        </button>
      </div>

      {error && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-950/30 dark:text-red-400">{error}</div>
      )}
    </div>
  );
}
