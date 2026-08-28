import { useState } from "react";
import { useSearchParams, useNavigate } from "react-router-dom";
import { useSources, useUpdateSources, useUpdateGmailTrackingMode, useGmailForwardingStatus } from "../hooks/useSources";
import { useSyncedState } from "../hooks/useSyncedState";
import { gmailOAuthStartUrl } from "../api";
import type { SourcesResponse } from "../types";
import { PageHeader, Surface, Well, Button, Eyebrow, Callout, SourceStatusTile } from "../ui";

const LABEL = "mb-2 flex items-center text-body font-[650] text-ink-2";

// Plain top-level function, not inlined into SourcesPage's handleSave below. react-hooks'
// bundled compiler diagnostics flag a direct `window.location.href = ...` assignment when
// it's textually inside a component function that also calls a useState setter elsewhere
// (handleSave does, for the non-onboarding path) — and unlike a normal lint rule, this one
// surfaces as a bare compiler error with no ruleId, so `eslint-disable-next-line` can't
// suppress it (tried it; ESLint reports the directive as unused and still fails). Moving the
// actual mutation into a plain function outside the component sidesteps it for real.
function hardNavigateHome() {
  window.location.href = "/";
}

// Only ever rendered once Gmail is connected — see useGmailForwardingStatus. Gmail won't
// let a third-party app add a NEW forwarding address for a personal account (a Google
// restriction, not a gap here), so this is a status check + auto-install, not a one-click
// setup — the one manual step happens in Gmail's own settings.
function GmailForwardingSetup() {
  const { data: status, loading, error, reload } = useGmailForwardingStatus();
  const [copied, setCopied] = useState(false);

  async function handleCopy() {
    if (!status) return;
    await navigator.clipboard.writeText(status.address);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  if (status?.status === "verified") {
    return (
      <div className="rounded-core bg-pos-wash p-5">
        <p className="text-body font-[650] text-pos">✓ Forwarding confirmed. The job-alert filter is installed automatically.</p>
      </div>
    );
  }

  return (
    <Surface padding="lg">
      <label className={LABEL}>Set up alert forwarding</label>
      <p className="mb-3 text-body text-muted">
        Gmail requires you to add this yourself. In Gmail, go to Settings → Forwarding and
        POP/IMAP → Add a forwarding address, paste the address below, then confirm it via
        the email Gmail sends you. Once confirmed, the app automatically installs a filter
        that forwards matching job alerts here, no manual filter setup needed.
      </p>
      {status && (
        <div className="mb-3 flex items-center gap-2">
          <Well className="px-3 py-2 text-body text-ink-2">{status.address}</Well>
          <Button variant="ghost" size="sm" onClick={handleCopy}>
            {copied ? "Copied!" : "Copy"}
          </Button>
        </div>
      )}
      <div className="flex items-center gap-3">
        <Button onClick={reload} disabled={loading}>
          {loading ? "Checking…" : "Check status"}
        </Button>
        <span className="text-body text-muted">
          {status?.status === "pending" ? "Waiting for you to confirm in Gmail" : status?.status === "not_added" ? "Not added yet" : ""}
        </span>
      </div>
      {error && <p className="mt-2 text-caption text-ember">{error}</p>}
    </Surface>
  );
}

// Trimmed to the prototype's terse one-liners. The fuller privacy disclosure this used to carry
// (AI-provider classification, the cancel-time delete/keep choice) still lives on the page: the
// Danger zone card covers the cancel-time choice, and the "read only" claim below is the one
// piece of that disclosure worth keeping inline no matter how short this gets.
const TRACKING_MODES: { value: "full" | "filter" | "manual"; label: string; description: string }[] = [
  {
    value: "full",
    label: "Full inbox access",
    description: "Read only. We keep only job related mail and clear the rest straight after checking.",
  },
  {
    value: "filter",
    label: "Filter only",
    description: "We never read your inbox. A Gmail filter forwards mail from companies you have applied to.",
  },
  {
    value: "manual",
    label: "Manual only",
    description: "No automatic tracking. You log applications and set status yourself.",
  },
];

// Independent of the alert-based sources above — a user can track application status without
// selecting any alert source, and vice versa. No mode is ever pre-selected: the user must
// actively choose, since "full" implies reading inbox content.
function GmailTrackingModeSection({ sources }: { sources: SourcesResponse }) {
  const { execute, loading } = useUpdateGmailTrackingMode();
  const [mode, setMode] = useState(sources.gmailTrackingMode);

  async function select(next: "full" | "filter" | "manual") {
    setMode(next);
    await execute(next);
  }

  return (
    <Surface padding="lg">
      <Eyebrow className="mb-[3px]">Application tracking</Eyebrow>
      <p className="mb-3 text-note text-faint">How we notice when a status changes. Pick whichever you are comfortable with.</p>
      <div className="grid grid-cols-1 gap-[9px] sm:grid-cols-3">
        {TRACKING_MODES.map(m => (
          <button
            key={m.value}
            type="button"
            disabled={loading}
            onClick={() => select(m.value)}
            className={`block w-full rounded-ctl p-3 text-left transition-colors duration-300 focus-ring tappable disabled:pointer-events-none disabled:opacity-55 ${
              mode === m.value ? "bg-ember-wash shadow-[inset_0_0_0_1px_var(--color-ember)]" : "surface-sunk hover:bg-shell"
            }`}
          >
            <p className={`text-body font-[650] ${mode === m.value ? "text-ember" : "text-ink-2"}`}>{m.label}</p>
            <p className="mt-0.5 text-caption text-faint">{m.description}</p>
          </button>
        ))}
      </div>

      {mode === "filter" && !sources.gmailConnected && (
        <div className="mt-3 flex items-center gap-3">
          <Button href={gmailOAuthStartUrl()}>Connect Gmail</Button>
          <span className="text-body text-muted">Needed to install per-company filters.</span>
        </div>
      )}
      {mode === "full" && !sources.gmailReadonlyConnected && (
        <div className="mt-3 flex items-center gap-3">
          <Button href={gmailOAuthStartUrl("full")}>Grant full inbox access</Button>
          <span className="text-body text-muted">Redirects to Google's consent screen.</span>
        </div>
      )}
      {mode === "full" && sources.gmailReadonlyConnected && (
        <p className="mt-3 text-body text-pos">✓ Connected, tracking automatically.</p>
      )}
    </Surface>
  );
}

export function SourcesPage({ hideHeader = false, onboarding = false }: { hideHeader?: boolean; onboarding?: boolean } = {}) {
  const { data, loading: loadingSources } = useSources();
  const [selected, setSelected] = useSyncedState<SourcesResponse, string[]>(data, [], d => d.enabled);
  const [saved, setSaved] = useState(false);
  const { execute, loading: saving, error } = useUpdateSources();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();

  function toggle(key: string) {
    setSelected(s => (s.includes(key) ? s.filter(k => k !== key) : [...s, key]));
    setSaved(false);
  }

  async function handleSave() {
    const result = await execute(selected);
    // During onboarding, always move on after a successful save, regardless of whether an
    // alert-based source was picked — connecting Gmail is an optional follow-up reachable
    // any time later from the persistent Sources page (nav), not a hard blocker to finishing
    // onboarding. There's no nav bar during onboarding to escape via otherwise, so staying on
    // this page when Gmail isn't connected yet was a genuine dead end. A full reload (not
    // SPA navigate) so the next /auth/me fetch picks up the now-saved needsSourceSelection
    // flag — same pattern the other onboarding steps use (ResumeIntakePage,
    // OnboardingCriteriaPage's CriteriaWizard onSaved) instead of react-router's navigate(),
    // whose target `me` is only fetched once on mount and would otherwise still look stale
    // and bounce straight back here via App.tsx's StepRedirect.
    if (onboarding) {
      hardNavigateHome();
      return;
    }
    setSelected(result.enabled);
    setSaved(true);
    // Nothing further needed if no alert-based source was chosen — move on to the
    // dashboard. Otherwise stay here so the Connect Gmail button below stays reachable.
    // alertKeys is declared below but already assigned by the time this ever runs — it's
    // only invoked from the onClick binding, after the render that declares it.
    if (!result.enabled.some(k => alertKeys.has(k))) navigate("/");
  }

  if (loadingSources) return <div className="py-12 text-center text-note text-faint">Loading…</div>;

  const automatic = data?.catalog.filter(c => c.automatic) ?? [];
  const alertBased = data?.catalog.filter(c => !c.automatic) ?? [];
  const alertKeys = new Set(alertBased.map(s => s.key));
  const needsGmail = selected.some(k => alertKeys.has(k));
  const gmailStatus = searchParams.get("gmail");

  return (
    <div className="space-y-6">
      {!hideHeader && (
        <PageHeader title="Choose your sources" tagline="Tell us where to look, and how you want applications tracked." />
      )}

      {gmailStatus === "connected" && (
        <div className="rounded-core bg-pos-wash p-4 text-body text-pos">Gmail connected.</div>
      )}
      {gmailStatus === "error" && <Callout variant="danger" title="Couldn't connect Gmail. Please try again." />}

      <Surface padding="lg">
        <Eyebrow className="mb-[3px]">Automatic</Eyebrow>
        <p className="mb-3 text-note text-faint">These need nothing from you. They run every night.</p>
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
          {automatic.map(s => (
            <SourceStatusTile key={s.key} label={s.label} active={selected.includes(s.key)} onClick={() => toggle(s.key)} />
          ))}
        </div>
      </Surface>

      <Surface padding="lg">
        <Eyebrow className="mb-[3px]">Alert based</Eyebrow>
        <p className="mb-3 text-note text-faint">Set an alert on the platform, then connect Gmail so it forwards in.</p>
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
          {alertBased.map(s => (
            <SourceStatusTile
              key={s.key}
              label={s.label}
              active={selected.includes(s.key)}
              offLabel="Needs setup"
              onClick={() => toggle(s.key)}
            />
          ))}
        </div>
      </Surface>

      {needsGmail && data?.gmailConnected && <GmailForwardingSetup />}

      {needsGmail && !data?.gmailConnected && (
        <Surface padding="lg">
          <label className={LABEL}>Connect Gmail</label>
          <p className="mb-3 text-body text-muted">
            Lets the app manage a filter that forwards matching job alerts to us. It can only
            manage filters and settings, never read your mail.
          </p>
          <Button href={gmailOAuthStartUrl()}>Connect Gmail</Button>
        </Surface>
      )}

      {data && <GmailTrackingModeSection sources={data} />}

      <div className="flex items-center gap-3">
        <Button onClick={handleSave} disabled={saving}>
          {saving ? "Saving…" : "Save sources"}
        </Button>
        {saved && <span className="text-body text-pos">Saved.</span>}
      </div>

      {error && <Callout variant="danger" title={error} />}
    </div>
  );
}
