import { useEffect, useState } from "react";
import { useProfile, useUpdateProfile, useParseResumePdf, useUploadResumePdf } from "../hooks/useProfile";
import { useCancelAccount } from "../hooks/useAuth";
import { resumePdfUrl } from "../api";
import { BackgroundEditor } from "../components/BackgroundEditor";
import { JobCriteriaEditor } from "../components/JobCriteriaEditor";
import { ResumePdfViewer } from "../components/ResumePdfViewer";
import { parseBackgroundYaml, serializeBackgroundYaml, type BackgroundParseResult } from "../lib/backgroundYaml";
import { parseJobCriteriaYaml, serializeJobCriteriaYaml, type JobCriteriaData } from "../lib/jobCriteriaYaml";

export function SettingsPage() {
  const { data: profile, loading: loadingProfile } = useProfile();
  const [background, setBackground] = useState<BackgroundParseResult | null>(null);
  const [jobCriteria, setJobCriteria] = useState<JobCriteriaData | null>(null);
  const [cvBase, setCvBase] = useState("");
  const [hasResumePdf, setHasResumePdf] = useState(false);
  // A picked-but-not-yet-saved replacement — previewed locally, only persisted on Save
  // (same reasoning as ResumeIntakePage: keep the stored PDF and stored CV text consistent
  // with each other rather than one landing ahead of the other).
  const [newResumeFile, setNewResumeFile] = useState<File | null>(null);
  const [updatedAt, setUpdatedAt] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  const save = useUpdateProfile();
  const cancel = useCancelAccount();
  const parsePdf = useParseResumePdf();
  const uploadPdf = useUploadResumePdf();

  useEffect(() => {
    if (!profile) return;
    setBackground(parseBackgroundYaml(profile.background));
    setJobCriteria(parseJobCriteriaYaml(profile.jobCriteria));
    setCvBase(profile.cvBase);
    setHasResumePdf(profile.hasResumePdf);
    setUpdatedAt(profile.updatedAt);
  }, [profile]);

  // Replacing the PDF only ever updates the CV text and file — it deliberately leaves
  // Background untouched. Background can carry hand-added detail (project write-ups, extra
  // roles) that a fresh resume parse would never reproduce; overwriting it just because the
  // user updated their CV's formatting would silently discard that.
  async function handleReplaceResume(file: File) {
    const result = await parsePdf.execute(file);
    setCvBase(result.cvBase);
    setNewResumeFile(file);
    setSaved(false);
  }

  async function handleSave() {
    if (!background || !jobCriteria) return;
    const backgroundYaml = background.ok ? serializeBackgroundYaml(background.data) : background.rawText;
    const [updated] = await Promise.all([
      save.execute({
        background: backgroundYaml,
        cvBase,
        jobCriteria: serializeJobCriteriaYaml(jobCriteria),
      }),
      newResumeFile ? uploadPdf.execute(newResumeFile) : Promise.resolve(),
    ]);
    if (newResumeFile) setHasResumePdf(true);
    setUpdatedAt(updated.updatedAt);
    setSaved(true);
  }

  async function handleCancelAccount() {
    if (!confirm("Cancel your account? You'll be signed out and won't be able to log back in unless it's reactivated. Your data is kept, not deleted.")) return;
    await cancel.execute();
    window.location.href = "/";
  }

  if (loadingProfile || !background || !jobCriteria) {
    return <div className="py-12 text-center text-sm text-gray-400">Loading…</div>;
  }

  const savingResume = parsePdf.loading || uploadPdf.loading;

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
        Edit your background and job criteria directly. Changes apply to the next CV, cover
        letter, answer, or posting evaluation you request — nothing here needs a separate re-run.
      </p>

      <div>
        <h3 className="mb-2 text-sm font-medium text-gray-700">Background</h3>
        <BackgroundEditor value={background} onChange={v => { setBackground(v); setSaved(false); }} />
      </div>

      <div>
        <h3 className="mb-2 text-sm font-medium text-gray-700">Base CV</h3>
        {newResumeFile ? (
          <ResumePdfViewer source={newResumeFile} />
        ) : hasResumePdf ? (
          <ResumePdfViewer source={resumePdfUrl()} />
        ) : (
          <p className="rounded-xl border border-gray-200 bg-gray-50 p-4 text-sm text-gray-500">
            No PDF on file — your base CV comes from pasted text.
          </p>
        )}
        <label className="mt-2 inline-block cursor-pointer text-sm font-medium text-blue-600 hover:text-blue-700">
          {parsePdf.loading ? "Reading PDF…" : "Replace resume PDF"}
          <input
            type="file"
            accept="application/pdf"
            className="hidden"
            disabled={savingResume}
            onChange={e => {
              const file = e.target.files?.[0];
              if (file) handleReplaceResume(file);
              e.target.value = "";
            }}
          />
        </label>
      </div>

      <div>
        <h3 className="mb-2 text-sm font-medium text-gray-700">Job criteria</h3>
        <JobCriteriaEditor value={jobCriteria} onChange={v => { setJobCriteria(v); setSaved(false); }} />
      </div>

      <div className="flex items-center gap-3">
        <button
          onClick={handleSave}
          disabled={save.loading || savingResume}
          className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:bg-gray-300"
        >
          {save.loading ? "Saving…" : "Save changes"}
        </button>
        {saved && <span className="text-sm text-emerald-600">Saved.</span>}
      </div>

      {(save.error ?? parsePdf.error ?? uploadPdf.error) && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          {save.error ?? parsePdf.error ?? uploadPdf.error}
        </div>
      )}

      <div className="rounded-xl border border-red-200 bg-white p-5 shadow-sm">
        <p className="mb-1 text-sm font-medium text-red-700">Danger zone</p>
        <p className="mb-3 text-sm text-gray-500">
          Cancels your account and signs you out. Your data is kept, not deleted, in case you
          come back — you just won't be able to sign in again unless it's reactivated.
        </p>
        <button
          onClick={handleCancelAccount}
          className="rounded-lg border border-red-200 px-4 py-2 text-sm font-medium text-red-700 transition-colors hover:bg-red-50"
        >
          Cancel my account
        </button>
        {cancel.error && <p className="mt-2 text-sm text-red-700">{cancel.error}</p>}
      </div>
    </div>
  );
}
