import { useProfile, useUpdateProfile } from "../hooks/useProfile";
import { useMe } from "../hooks/useAuth";
import { useSyncedState } from "../hooks/useSyncedState";
import { JobCriteriaEditor } from "../components/JobCriteriaEditor";
import { parseJobCriteriaYaml, serializeJobCriteriaYaml, type JobCriteriaData } from "../lib/jobCriteriaYaml";
import { getMissingCriteriaFields } from "../lib/criteriaCompleteness";
import { PageHeader, Button, Callout } from "../ui";

const EMPTY: JobCriteriaData = parseJobCriteriaYaml("");

export function JobCriteriaPage({ hideHeader = false, onSaved }: { hideHeader?: boolean; onSaved?: () => void } = {}) {
  const { data: profile, loading: loadingProfile } = useProfile();
  const { data: me } = useMe();
  // Reflects whatever's already saved rather than always starting from defaults — editing
  // and saving from a blank slate would otherwise silently wipe out existing criteria.
  const [criteria, setCriteria] = useSyncedState(profile, EMPTY, p => parseJobCriteriaYaml(p.jobCriteria));
  const { execute, loading: saving, error } = useUpdateProfile();

  // Full reload rather than re-setting local state — editing the Advanced (raw YAML) box
  // only updates the `extra` bucket locally, it doesn't re-derive the structured fields
  // from whatever was just saved. A reload re-fetches and re-parses the real saved value,
  // so the page always shows exactly what was persisted.
  async function handleSave() {
    await execute({ jobCriteria: serializeJobCriteriaYaml(criteria) });
    if (onSaved) onSaved();
    else window.location.reload();
  }

  if (loadingProfile) return <div className="py-12 text-center text-note text-faint">Loading…</div>;

  const missing = getMissingCriteriaFields(criteria, me?.tier ?? "Tier1");

  return (
    <div className="space-y-6">
      {!hideHeader && (
        <PageHeader title="Job criteria" tagline="What you're actually looking for, precise enough to tell a good match from a bad one." />
      )}

      <JobCriteriaEditor value={criteria} onChange={setCriteria} tier={me?.tier ?? "Tier1"} />

      {missing.length > 0 && (
        <Callout variant="warning" title={`Still needed before you can save: ${missing.map(m => m.label).join(", ")}.`} />
      )}

      <div className="flex items-center gap-3">
        <Button onClick={handleSave} disabled={saving || missing.length > 0}>
          {saving ? "Saving…" : "Save criteria"}
        </Button>
      </div>

      {error && <Callout variant="danger" title={error} />}
    </div>
  );
}
