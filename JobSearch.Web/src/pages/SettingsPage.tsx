import { useState } from "react";
import { useProfile, useUpdateProfile, useParseResumePdf, useUploadResumePdf } from "../hooks/useProfile";
import { useMe, useCancelAccount, useUpgradeToTier2, useInviteToTier2 } from "../hooks/useAuth";
import { useSyncedState } from "../hooks/useSyncedState";
import { resumePdfUrl } from "../api";
import { BackgroundEditor } from "../components/BackgroundEditor";
import { JobCriteriaEditor } from "../components/JobCriteriaEditor";
import { ResumePdfViewer } from "../components/ResumePdfViewer";
import { parseBackgroundYaml, serializeBackgroundYaml, type BackgroundParseResult } from "../lib/backgroundYaml";
import { parseJobCriteriaYaml, serializeJobCriteriaYaml, type JobCriteriaData } from "../lib/jobCriteriaYaml";
import { PageTagline } from "../components/PageTagline";
import { CARD, PRIMARY_BUTTON } from "../lib/styles";
import type { Profile } from "../types";

export function SettingsPage() {
  const { data: profile, loading: loadingProfile } = useProfile();
  // Each of these five fields hydrates from the same `profile` load, then edits locally from
  // there — same idiom as JobCriteriaPage/CriteriaWizard/ResumeBuilderPage, just fanned out
  // across more than one piece of local state.
  const [background, setBackground] = useSyncedState<Profile, BackgroundParseResult | null>(profile, null, p => parseBackgroundYaml(p.background));
  const [jobCriteria, setJobCriteria] = useSyncedState<Profile, JobCriteriaData | null>(profile, null, p => parseJobCriteriaYaml(p.jobCriteria));
  const [cvBase, setCvBase] = useSyncedState<Profile, string>(profile, "", p => p.cvBase);
  const [hasResumePdf] = useSyncedState<Profile, boolean>(profile, false, p => p.hasResumePdf);
  const [updatedAt] = useSyncedState<Profile, string | null>(profile, null, p => p.updatedAt);
  // A picked-but-not-yet-saved replacement — previewed locally, only persisted on Save
  // (same reasoning as ResumeIntakePage: keep the stored PDF and stored CV text consistent
  // with each other rather than one landing ahead of the other).
  const [newResumeFile, setNewResumeFile] = useState<File | null>(null);
  const [inviteEmail, setInviteEmail] = useState("");
  const [inviteResult, setInviteResult] = useState<{ email: string; emailSent: boolean } | null>(null);
  const [confirmingCancel, setConfirmingCancel] = useState(false);
  // Deleting is the default — matches what actually happens if this choice screen is somehow
  // skipped, and "your data is gone unless you explicitly ask to keep it" is the safer
  // default to land on than the reverse.
  const [deleteDataOnCancel, setDeleteDataOnCancel] = useState(true);

  const { data: me } = useMe();
  const save = useUpdateProfile();
  const cancel = useCancelAccount();
  const upgrade = useUpgradeToTier2();
  const invite = useInviteToTier2();
  const parsePdf = useParseResumePdf();
  const uploadPdf = useUploadResumePdf();

  // Replacing the PDF only ever updates the CV text and file — it deliberately leaves
  // Background untouched. Background can carry hand-added detail (project write-ups, extra
  // roles) that a fresh resume parse would never reproduce; overwriting it just because the
  // user updated their CV's formatting would silently discard that.
  async function handleReplaceResume(file: File) {
    const result = await parsePdf.execute(file);
    setCvBase(result.cvBase);
    setNewResumeFile(file);
  }

  // Full reload rather than re-setting local state — editing the Advanced (raw YAML) box
  // on either editor only updates the `extra` bucket locally, it doesn't re-derive the
  // structured fields from whatever was just saved. A reload re-fetches and re-parses the
  // real saved value, so the page always shows exactly what was persisted.
  async function handleSave() {
    if (!background || !jobCriteria) return;
    const backgroundYaml = background.ok ? serializeBackgroundYaml(background.data) : background.rawText;
    await Promise.all([
      save.execute({
        background: backgroundYaml,
        cvBase,
        jobCriteria: serializeJobCriteriaYaml(jobCriteria),
      }),
      newResumeFile ? uploadPdf.execute(newResumeFile) : Promise.resolve(),
    ]);
    window.location.reload();
  }

  async function handleCancelAccount() {
    await cancel.execute(deleteDataOnCancel);
    window.location.href = "/";
  }

  // Hard redirect, not client-side navigate — useMe() only fetches /auth/me once on mount,
  // same reasoning as handleCancelAccount above, so a tier change needs a fresh page load
  // to actually take effect anywhere that reads it (nav bar, the /sources funnel gate).
  async function handleUpgrade() {
    await upgrade.execute();
    window.location.href = "/sources";
  }

  async function handleInvite() {
    const result = await invite.execute(inviteEmail);
    setInviteResult(result);
    setInviteEmail("");
  }

  if (loadingProfile || !background || !jobCriteria) {
    return <div className="py-12 text-center text-sm text-gray-400 dark:text-gray-500">Loading…</div>;
  }

  const savingResume = parsePdf.loading || uploadPdf.loading;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold text-gray-700 dark:text-gray-200">Settings</h2>
        {updatedAt && (
          <span className="text-xs text-gray-400 dark:text-gray-500">
            Last updated {new Date(updatedAt).toLocaleString("en-AU")}
          </span>
        )}
      </div>
      <PageTagline>Everything about you, and everything about your account, in one place.</PageTagline>
      <p className="text-sm text-gray-500 dark:text-gray-400">
        Edit your background and job criteria directly. Changes apply to the next CV, cover
        letter, answer, or posting evaluation you request. Nothing here needs a separate re-run.
      </p>

      <div>
        <h3 className="mb-2 text-sm font-medium text-gray-700 dark:text-gray-200">Background</h3>
        <BackgroundEditor value={background} onChange={setBackground} />
      </div>

      <div>
        <h3 className="mb-2 text-sm font-medium text-gray-700 dark:text-gray-200">Base CV</h3>
        {newResumeFile ? (
          <ResumePdfViewer source={newResumeFile} />
        ) : hasResumePdf ? (
          <ResumePdfViewer source={resumePdfUrl()} />
        ) : (
          <p className="rounded-xl border border-gray-200 bg-gray-50 p-4 text-sm text-gray-500 dark:border-gray-800 dark:bg-gray-900 dark:text-gray-400">
            No PDF on file. Your base CV comes from pasted text.
          </p>
        )}
        <label className="mt-2 inline-block cursor-pointer text-sm font-medium text-violet-600 transition-colors hover:text-violet-700 dark:text-violet-400 dark:hover:text-violet-300">
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
        <h3 className="mb-2 text-sm font-medium text-gray-700 dark:text-gray-200">Job criteria</h3>
        <JobCriteriaEditor value={jobCriteria} onChange={setJobCriteria} tier={me?.tier ?? "Tier1"} />
      </div>

      <div className="flex items-center gap-3">
        <button onClick={handleSave} disabled={save.loading || savingResume} className={PRIMARY_BUTTON}>
          {save.loading ? "Saving…" : "Save changes"}
        </button>
      </div>

      {(save.error ?? parsePdf.error ?? uploadPdf.error) && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-950/30 dark:text-red-400">
          {save.error ?? parsePdf.error ?? uploadPdf.error}
        </div>
      )}

      {me?.tier === "Tier1" && (
        <div className="rounded-xl border border-violet-200 bg-gradient-to-br from-violet-50 to-fuchsia-50 p-5 shadow-sm dark:border-violet-900/50 dark:from-violet-950/40 dark:to-fuchsia-950/40">
          <p className="mb-1 text-sm font-medium text-violet-700 dark:text-violet-300">Tier 2 (Beta)</p>
          <p className="mb-3 text-sm text-gray-600 dark:text-gray-300">
            Unlock automatic job discovery, application tracking, and inbox alert forwarding.
            Free while the beta is running, no payment required yet.
          </p>
          <button onClick={handleUpgrade} disabled={upgrade.loading} className={PRIMARY_BUTTON}>
            {upgrade.loading ? "Upgrading…" : "Upgrade to Tier 2"}
          </button>
          {upgrade.error && <p className="mt-2 text-sm text-red-700 dark:text-red-400">{upgrade.error}</p>}
        </div>
      )}

      {me?.isOwner && (
        <div className={CARD}>
          <p className="mb-1 text-sm font-medium text-gray-700 dark:text-gray-200">Invite to Tier 2</p>
          <p className="mb-3 text-sm text-gray-500 dark:text-gray-400">
            Grants an email sign-in access and lands them straight at Tier 2. Sends them an
            email if SendGrid's configured; otherwise still adds them, just let them know
            another way.
          </p>
          <div className="flex items-center gap-3">
            <input
              type="email"
              value={inviteEmail}
              onChange={e => setInviteEmail(e.target.value)}
              placeholder="someone@example.com"
              className="w-full max-w-xs rounded-lg border border-gray-200 bg-white p-2 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-violet-400 dark:border-gray-700 dark:bg-gray-800 dark:text-gray-100 dark:focus:ring-violet-500"
            />
            <button onClick={handleInvite} disabled={invite.loading || !inviteEmail.trim()} className={PRIMARY_BUTTON}>
              {invite.loading ? "Inviting…" : "Invite"}
            </button>
          </div>
          {inviteResult && (
            <p className="mt-2 text-sm text-emerald-600 dark:text-emerald-400">
              {inviteResult.email} can now sign in.
              {inviteResult.emailSent ? " Invite email sent." : " (Email not sent, SendGrid not configured yet, let them know another way.)"}
            </p>
          )}
          {invite.error && <p className="mt-2 text-sm text-red-700 dark:text-red-400">{invite.error}</p>}
        </div>
      )}

      <div className="rounded-xl border border-red-200 bg-white p-5 shadow-sm dark:border-red-900/50 dark:bg-gray-900">
        <p className="mb-1 text-sm font-medium text-red-700 dark:text-red-400">Danger zone</p>
        <p className="mb-3 text-sm text-gray-500 dark:text-gray-400">
          Cancels your account and signs you out. Gmail access is disconnected either way —
          we revoke it with Google directly, not just stop using it. You just won't be able to
          sign in again unless it's reactivated.
        </p>

        {!confirmingCancel ? (
          <button
            onClick={() => setConfirmingCancel(true)}
            className="rounded-lg border border-red-200 px-4 py-2 text-sm font-medium text-red-700 transition-colors duration-150 hover:bg-red-50 dark:border-red-900/50 dark:text-red-400 dark:hover:bg-red-950/30"
          >
            Cancel my account
          </button>
        ) : (
          <div className="space-y-3">
            <div className="space-y-2">
              {(
                [
                  { value: true, label: "Delete my data", description: "Removes your tracked application history and everything derived from your inbox. Starting again later means starting fresh." },
                  { value: false, label: "Keep my data", description: "In case you come back — your tracked application history stays, ready to pick up where you left off." },
                ] as const
              ).map(opt => (
                <button
                  key={String(opt.value)}
                  type="button"
                  onClick={() => setDeleteDataOnCancel(opt.value)}
                  className={`block w-full rounded-lg border p-3 text-left transition-colors duration-150 ${
                    deleteDataOnCancel === opt.value
                      ? "border-violet-300 bg-violet-50 dark:border-violet-700 dark:bg-violet-500/10"
                      : "border-gray-200 bg-white hover:bg-gray-50 dark:border-gray-700 dark:bg-gray-900 dark:hover:bg-gray-800"
                  }`}
                >
                  <p className={`text-sm font-medium ${deleteDataOnCancel === opt.value ? "text-violet-700 dark:text-violet-300" : "text-gray-700 dark:text-gray-200"}`}>
                    {opt.label}
                  </p>
                  <p className="mt-0.5 text-xs text-gray-500 dark:text-gray-400">{opt.description}</p>
                </button>
              ))}
            </div>
            <div className="flex items-center gap-3">
              <button
                onClick={handleCancelAccount}
                disabled={cancel.loading}
                className="rounded-lg bg-red-600 px-4 py-2 text-sm font-medium text-white transition-colors duration-150 hover:bg-red-700 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {cancel.loading ? "Cancelling…" : "Confirm cancellation"}
              </button>
              <button
                onClick={() => setConfirmingCancel(false)}
                className="rounded-lg px-3 py-2 text-sm font-medium text-gray-500 transition-colors hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-800"
              >
                Never mind
              </button>
            </div>
          </div>
        )}
        {cancel.error && <p className="mt-2 text-sm text-red-700 dark:text-red-400">{cancel.error}</p>}
      </div>
    </div>
  );
}
