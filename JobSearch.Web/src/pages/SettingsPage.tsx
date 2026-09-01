import { useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useProfile, useUpdateProfile, useParseResumePdf, useUploadResumePdf } from "../hooks/useProfile";
import { useMe, useCancelAccount, useUpgradeToTier2, useDowngradeToTier1, useInviteToTier2 } from "../hooks/useAuth";
import { useSyncedState } from "../hooks/useSyncedState";
import { resumePdfUrl } from "../api";
import { BackgroundEditor } from "../components/BackgroundEditor";
import { ResumePdfViewer } from "../components/ResumePdfViewer";
import { parseBackgroundYaml, serializeBackgroundYaml, type BackgroundParseResult } from "../lib/backgroundYaml";
import { isSettingsTab, type SettingsTab } from "../lib/settingsTab";
import {
  PageHeader,
  Surface,
  Button,
  Callout,
  Input,
  SegmentedControl,
  ProgressBar,
  Eyebrow,
  SettingsSubNav,
  useTheme,
  type SettingsSubNavItem,
} from "../ui";
import type { Profile } from "../types";

// The prototype's own sub-nav points every one of its non-tab items at itself (it is a single
// static page), so which of Account/Resume/Billing become local tabs vs. real links is a product
// call the prototype does not settle on its own:
//  - Criteria and Sources already have full pages of their own (/criteria, /sources) with editors
//    this page must not duplicate — those items navigate away for real.
//  - Help already has its own route too, same reasoning.
//  - Account, Resume and Billing have no page of their own. Resume shows Background + Base CV
//    (previously inline on this page anyway). Billing aliases into Account rather than being a
//    fourth panel: the only billing-relevant content that exists — Plan, Credits, the Tier 2
//    upgrade/invite cards — already lives in Account, and inventing separate Billing content not
//    shown anywhere in the source design would be guessing.
// (SettingsTab itself, and the ?tab= query-param guard isSettingsTab, live in lib/settingsTab.ts
// so the guard can be unit-tested without dragging in this page's whole component tree.)

// Exported so SettingsShell (the wrapper Criteria/Sources/Help render themselves in) can show
// the exact same nav instead of duplicating this list.
export const SUB_NAV_ITEMS: SettingsSubNavItem[] = [
  { key: "account", label: "Account" },
  { key: "resume", label: "Resume" },
  { key: "criteria", label: "Criteria", href: "/criteria" },
  { key: "sources", label: "Sources", href: "/sources" },
  { key: "billing", label: "Billing" },
  { key: "help", label: "Help", href: "/help" },
];

// `creditBalance` is a running total the API exposes today, not a per-period quota — there is no
// "monthly allotment" field to divide by. This is a display-only ceiling for the progress bar so
// it reads as a meter rather than nothing; swap it for a real plan allotment once the API has one.
const CREDIT_DISPLAY_CEILING = 200;

export function SettingsPage() {
  const { data: profile, loading: loadingProfile } = useProfile();
  const [background, setBackground] = useSyncedState<Profile, BackgroundParseResult | null>(profile, null, p => parseBackgroundYaml(p.background));
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
  const [confirmingDowngrade, setConfirmingDowngrade] = useState(false);
  // Deleting is the default — matches what actually happens if this choice screen is somehow
  // skipped, and "your data is gone unless you explicitly ask to keep it" is the safer
  // default to land on than the reverse.
  const [deleteDataOnCancel, setDeleteDataOnCancel] = useState(true);
  const [searchParams] = useSearchParams();
  // Seeded once from ?tab=, the query param SettingsShell navigates back with when a local-tab
  // item (Account/Resume/Billing) is clicked from /criteria, /sources or /help — see that
  // component's own comment. Not re-derived on every searchParams change, same reasoning as
  // DiscoveriesPage's own ?posting= deep-link: this only needs to act once per visit. Falls back
  // to "account" for a missing or untrusted value rather than trusting the query string blindly.
  const [tab, setTab] = useState<SettingsTab>(() => {
    const raw = searchParams.get("tab");
    return isSettingsTab(raw) ? raw : "account";
  });
  // "billing" shows the same panel as "account" (see the SUB_NAV_ITEMS comment above) — this is
  // only where they diverge: which nav item highlights as active.
  const activePanel: "account" | "resume" = tab === "resume" ? "resume" : "account";

  const { data: me } = useMe();
  const { preference, setPreference } = useTheme();
  const save = useUpdateProfile();
  const cancel = useCancelAccount();
  const upgrade = useUpgradeToTier2();
  const downgrade = useDowngradeToTier1();
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
  // on the background editor only updates the `extra` bucket locally, it doesn't re-derive the
  // structured fields from whatever was just saved. A reload re-fetches and re-parses the
  // real saved value, so the page always shows exactly what was persisted.
  async function handleSave() {
    if (!background) return;
    const backgroundYaml = background.ok ? serializeBackgroundYaml(background.data) : background.rawText;
    await Promise.all([
      // jobCriteria is deliberately omitted — it is Partial, and editing it now lives at
      // /criteria only, not here. Omitting the field leaves whatever is already saved alone.
      save.execute({ background: backgroundYaml, cvBase }),
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

  // Same hard-reload reasoning as handleUpgrade/handleCancelAccount — useMe() only fetches
  // once on mount, so the new tier (and the just-forfeited credit balance) needs a fresh
  // page load to show up anywhere that reads it. Reload rather than a redirect: unlike
  // upgrade there's no dedicated downgrade destination, and the user is already looking at
  // the page that shows the result (Plan, Credits above).
  async function handleDowngrade() {
    await downgrade.execute();
    window.location.reload();
  }

  async function handleInvite() {
    const result = await invite.execute(inviteEmail);
    setInviteResult(result);
    setInviteEmail("");
  }

  if (loadingProfile || !background) {
    return <div className="py-12 text-center text-note text-faint">Loading…</div>;
  }

  const savingResume = parsePdf.loading || uploadPdf.loading;
  // No update-email endpoint exists yet, so Email stays read-only alongside Plan rather than
  // looking editable and silently doing nothing on change — a small departure from the
  // prototype, which does not mark that field readonly.
  const planLabel = me?.tier === "Tier2" ? "Tier 2, beta" : "Tier 1";

  return (
    <div className="space-y-6">
      <PageHeader
        title="Settings"
        tagline="Everything about you, and everything about your account, in one place."
        actions={updatedAt && (
          <span className="text-caption text-faint">Last updated {new Date(updatedAt).toLocaleString("en-AU")}</span>
        )}
      />

      <div className="grid grid-cols-1 items-start gap-3.5 md:grid-cols-[200px_1fr]">
        <SettingsSubNav items={SUB_NAV_ITEMS} activeKey={tab} onSelect={key => setTab(key as SettingsTab)} />

        <div className="min-w-0 space-y-3.5">
          {activePanel === "account" && (
            <>
              <Surface padding="lg">
                <Eyebrow className="mb-2.5">Account</Eyebrow>
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                  <Input label="Email" type="email" value={me?.email ?? ""} readOnly disabled />
                  <Input label="Plan" value={planLabel} readOnly disabled />
                </div>
                <div className="mt-3.5 flex flex-wrap items-center justify-between gap-3 pt-3 hairline-t">
                  <div>
                    <div className="text-body font-[650] text-ink-2">Appearance</div>
                    <div className="text-meta text-faint">Follows your system unless you choose</div>
                  </div>
                  <SegmentedControl
                    label="Appearance"
                    segments={[
                      { value: "system", label: "System" },
                      { value: "light", label: "Light" },
                      { value: "dark", label: "Dark" },
                    ]}
                    value={preference}
                    onChange={setPreference}
                  />
                </div>
              </Surface>

              <Surface padding="lg">
                <Eyebrow className="mb-2.5">Credits</Eyebrow>
                <div className="flex items-end justify-between gap-3">
                  <div>
                    <div className="text-stat font-bold text-ink">{me?.creditBalance ?? 0}</div>
                    <div className="mt-0.5 text-meta text-faint">Remaining this month</div>
                  </div>
                  {/* No top-up flow exists yet (no endpoint in api.ts) — same "affordance present,
                      not yet wired" treatment the gallery's own EmptyState uses for this button. */}
                  <Button variant="ghost" size="sm">
                    Top up
                  </Button>
                </div>
                <ProgressBar value={me?.creditBalance ?? 0} max={CREDIT_DISPLAY_CEILING} label="Credits remaining" className="mt-3" />
              </Surface>

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

              {me?.tier === "Tier2" && (
                <Surface padding="lg">
                  <p className="mb-1 text-body font-[650] text-ink-2">Downgrade to Tier 1</p>
                  <p className="mb-3 text-body text-muted">
                    Turns off automatic job discovery, application tracking, and inbox alert
                    forwarding. You can upgrade back to Tier 2 any time while the beta is running.
                  </p>

                  {!confirmingDowngrade ? (
                    <Button variant="ghost" onClick={() => setConfirmingDowngrade(true)}>
                      Downgrade to Tier 1
                    </Button>
                  ) : (
                    <div className="space-y-3">
                      <p className="text-body text-ember">
                        {(me?.creditBalance ?? 0) > 0
                          ? `This forfeits your remaining ${me?.creditBalance} credit${me?.creditBalance === 1 ? "" : "s"} immediately — they don't carry over to Tier 1.`
                          : "You have no unused credits, so nothing is forfeited."}
                      </p>
                      <div className="flex items-center gap-3">
                        <Button onClick={handleDowngrade} disabled={downgrade.loading}>
                          {downgrade.loading ? "Downgrading…" : "Confirm downgrade"}
                        </Button>
                        <Button variant="ghost" onClick={() => setConfirmingDowngrade(false)}>
                          Never mind
                        </Button>
                      </div>
                    </div>
                  )}
                  {downgrade.error && <p className="mt-2 text-caption text-ember">{downgrade.error}</p>}
                </Surface>
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
                <p className="mb-1 text-eyebrow text-ember uppercase">Danger zone</p>
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
            </>
          )}

          {activePanel === "resume" && (
            <>
              <Surface padding="lg">
                <Eyebrow className="mb-2.5">Background</Eyebrow>
                <BackgroundEditor value={background} onChange={setBackground} />
              </Surface>

              <Surface padding="lg">
                <Eyebrow className="mb-2.5">Base CV</Eyebrow>
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
              </Surface>

              <div className="flex items-center gap-3">
                <Button onClick={handleSave} disabled={save.loading || savingResume}>
                  {save.loading ? "Saving…" : "Save changes"}
                </Button>
              </div>

              {(save.error ?? parsePdf.error ?? uploadPdf.error) && (
                <Callout variant="danger" title={save.error ?? parsePdf.error ?? uploadPdf.error ?? ""} />
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
}
