import { useRef, useState } from "react";
import { useProfile, useUpdateProfile, useParseResumePdf, useUploadResumePdf } from "../hooks/useProfile";
import { useMe, useCancelAccount, useUpgradeToTier2, useInviteToTier2 } from "../hooks/useAuth";
import { useSyncedState } from "../hooks/useSyncedState";
import { resumePdfUrl } from "../api";
import { BackgroundEditor } from "../components/BackgroundEditor";
import { JobCriteriaEditor } from "../components/JobCriteriaEditor";
import { ResumePdfViewer } from "../components/ResumePdfViewer";
import { parseBackgroundYaml, serializeBackgroundYaml, type BackgroundParseResult } from "../lib/backgroundYaml";
import { parseJobCriteriaYaml, serializeJobCriteriaYaml, type JobCriteriaData } from "../lib/jobCriteriaYaml";
import { PageHeader, Surface, Button, Callout, Input } from "../ui";
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
  const replaceResumeInputRef = useRef<HTMLInputElement>(null);
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
    return <div className="py-12 text-center text-note text-faint">Loading…</div>;
  }

  const savingResume = parsePdf.loading || uploadPdf.loading;

  return (
    <div className="space-y-6">
      <PageHeader
        title="Settings"
        tagline="Everything about you, and everything about your account, in one place."
        actions={updatedAt && (
          <span className="text-caption text-faint">Last updated {new Date(updatedAt).toLocaleString("en-AU")}</span>
        )}
      />
      <p className="text-body text-muted">
        Edit your background and job criteria directly. Changes apply to the next CV, cover
        letter, answer, or posting evaluation you request. Nothing here needs a separate re-run.
      </p>

      <div>
        <h3 className="mb-2 text-body font-[650] text-ink-2">Background</h3>
        <BackgroundEditor value={background} onChange={setBackground} />
      </div>

      <div>
        <h3 className="mb-2 text-body font-[650] text-ink-2">Base CV</h3>
        {newResumeFile ? (
          <ResumePdfViewer source={newResumeFile} />
        ) : hasResumePdf ? (
          <ResumePdfViewer source={resumePdfUrl()} />
        ) : (
          <Callout variant="info" title="No PDF on file.">Your base CV comes from pasted text.</Callout>
        )}
        <input
          ref={replaceResumeInputRef}
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
        <button
          type="button"
          disabled={savingResume}
          onClick={() => replaceResumeInputRef.current?.click()}
          className="mt-2 text-note font-[650] text-ember transition-colors hover:text-ember-hi focus-ring rounded-ctl disabled:pointer-events-none disabled:opacity-55"
        >
          {parsePdf.loading ? "Reading PDF…" : "Replace resume PDF"}
        </button>
      </div>

      <div>
        <h3 className="mb-2 text-body font-[650] text-ink-2">Job criteria</h3>
        <JobCriteriaEditor value={jobCriteria} onChange={setJobCriteria} tier={me?.tier ?? "Tier1"} />
      </div>

      <div className="flex items-center gap-3">
        <Button onClick={handleSave} disabled={save.loading || savingResume}>
          {save.loading ? "Saving…" : "Save changes"}
        </Button>
      </div>

      {(save.error ?? parsePdf.error ?? uploadPdf.error) && (
        <Callout variant="danger" title={save.error ?? parsePdf.error ?? uploadPdf.error ?? ""} />
      )}

      {me?.tier === "Tier1" && (
        <div className="rounded-core bg-ember-wash p-5">
          <p className="mb-1 text-body font-[650] text-ember">Tier 2 (Beta)</p>
          <p className="mb-3 text-body text-ink-2">
            Unlock automatic job discovery, application tracking, and inbox alert forwarding.
            Free while the beta is running, no payment required yet.
          </p>
          <Button onClick={handleUpgrade} disabled={upgrade.loading}>
            {upgrade.loading ? "Upgrading…" : "Upgrade to Tier 2"}
          </Button>
          {upgrade.error && <p className="mt-2 text-caption text-ember">{upgrade.error}</p>}
        </div>
      )}

      {me?.isOwner && (
        <Surface padding="lg">
          <p className="mb-1 text-body font-[650] text-ink-2">Invite to Tier 2</p>
          <p className="mb-3 text-body text-muted">
            Grants an email sign-in access and lands them straight at Tier 2. Sends them an
            email if SendGrid's configured; otherwise still adds them, just let them know
            another way.
          </p>
          <div className="flex items-end gap-3">
            <Input
              label="Email"
              type="email"
              value={inviteEmail}
              onChange={e => setInviteEmail(e.target.value)}
              placeholder="someone@example.com"
              className="max-w-xs"
            />
            <Button onClick={handleInvite} disabled={invite.loading || !inviteEmail.trim()}>
              {invite.loading ? "Inviting…" : "Invite"}
            </Button>
          </div>
          {inviteResult && (
            <p className="mt-2 text-body text-pos">
              {inviteResult.email} can now sign in.
              {inviteResult.emailSent ? " Invite email sent." : " (Email not sent, SendGrid not configured yet, let them know another way.)"}
            </p>
          )}
          {invite.error && <p className="mt-2 text-caption text-ember">{invite.error}</p>}
        </Surface>
      )}

      <Surface padding="lg">
        <p className="mb-1 text-body font-[650] text-ember">Danger zone</p>
        <p className="mb-3 text-body text-muted">
          Cancels your account and signs you out. Gmail access is disconnected either way —
          we revoke it with Google directly, not just stop using it. You just won't be able to
          sign in again unless it's reactivated.
        </p>

        {!confirmingCancel ? (
          <Button variant="ghost" onClick={() => setConfirmingCancel(true)}>
            Cancel my account
          </Button>
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
                  className={`block w-full rounded-ctl p-3 text-left transition-colors duration-300 focus-ring tappable ${
                    deleteDataOnCancel === opt.value ? "bg-ember-wash shadow-[inset_0_0_0_1px_var(--color-ember)]" : "surface-sunk hover:bg-shell"
                  }`}
                >
                  <p className={`text-body font-[650] ${deleteDataOnCancel === opt.value ? "text-ember" : "text-ink-2"}`}>
                    {opt.label}
                  </p>
                  <p className="mt-0.5 text-caption text-faint">{opt.description}</p>
                </button>
              ))}
            </div>
            <div className="flex items-center gap-3">
              <Button onClick={handleCancelAccount} disabled={cancel.loading}>
                {cancel.loading ? "Cancelling…" : "Confirm cancellation"}
              </Button>
              <Button variant="ghost" onClick={() => setConfirmingCancel(false)}>
                Never mind
              </Button>
            </div>
          </div>
        )}
        {cancel.error && <p className="mt-2 text-caption text-ember">{cancel.error}</p>}
      </Surface>
    </div>
  );
}
