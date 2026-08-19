import { useEffect, useState } from "react";
import { useSearchParams, useNavigate } from "react-router-dom";
import { useSources, useUpdateSources, useUpdateGmailTrackingMode, useGmailForwardingStatus } from "../hooks/useSources";
import { gmailOAuthStartUrl } from "../api";
import type { SourcesResponse } from "../types";
import { InfoTooltip } from "../components/InfoTooltip";

const LABEL = "mb-2 block text-sm font-medium text-gray-700";

function SourceToggle({ label, active, onClick }: { label: string; active: boolean; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`rounded-lg px-3 py-1.5 text-sm font-medium transition-colors ${
        active ? "bg-blue-50 text-blue-700" : "bg-gray-100 text-gray-500 hover:bg-gray-200"
      }`}
    >
      {label}
    </button>
  );
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
      <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-5 shadow-sm">
        <p className="text-sm font-medium text-emerald-700">
          ✓ Forwarding confirmed. The job-alert filter is installed automatically.
        </p>
      </div>
    );
  }

  return (
    <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
      <label className={LABEL}>Set up alert forwarding</label>
      <p className="mb-3 text-sm text-gray-500">
        Gmail requires you to add this yourself. In Gmail, go to Settings → Forwarding and
        POP/IMAP → Add a forwarding address, paste the address below, then confirm it via
        the email Gmail sends you. Once confirmed, the app automatically installs a filter
        that forwards matching job alerts here, no manual filter setup needed.
      </p>
      {status && (
        <div className="mb-3 flex items-center gap-2">
          <code className="rounded-lg bg-gray-100 px-3 py-2 text-sm text-gray-700">{status.address}</code>
          <button
            onClick={handleCopy}
            className="rounded-lg px-3 py-2 text-sm font-medium text-blue-600 hover:bg-blue-50"
          >
            {copied ? "Copied!" : "Copy"}
          </button>
        </div>
      )}
      <div className="flex items-center gap-3">
        <button
          onClick={reload}
          disabled={loading}
          className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:bg-gray-300"
        >
          {loading ? "Checking…" : "Check status"}
        </button>
        <span className="text-sm text-gray-500">
          {status?.status === "pending" ? "Waiting for you to confirm in Gmail" : status?.status === "not_added" ? "Not added yet" : ""}
        </span>
      </div>
      {error && <p className="mt-2 text-sm text-red-700">{error}</p>}
    </div>
  );
}

const TRACKING_MODES: { value: "full" | "filter" | "manual"; label: string; description: string }[] = [
  {
    value: "full",
    label: "Full inbox access",
    description:
      "We read your inbox to catch application status changes automatically, regardless of " +
      "which company emails you. This is the most complete option, and the best experience if " +
      "you're comfortable granting it.",
  },
  {
    value: "filter",
    label: "Filter only (no inbox access)",
    description:
      "We never read your inbox. When you log an application, we install a Gmail filter that " +
      "simply forwards mail from that company's domain to your in-app address, the same " +
      "mechanism your job alerts already use. Less automatic: misses anything from a " +
      "different domain, and can't tell a rejection from an interview invite by content alone.",
  },
  {
    value: "manual",
    label: "Manual only",
    description: "No automatic tracking at all. You log applications and update their status yourself.",
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
    <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
      <label className={LABEL}>
        Application status tracking
        <InfoTooltip text="This choice only affects how status changes (like a rejection or an interview invite) get detected automatically. You can always update statuses yourself either way." />
      </label>
      <p className="mb-3 text-sm text-gray-500">
        How should we track the status of jobs you've applied to? Pick whichever you're
        comfortable with.
      </p>
      <div className="space-y-2">
        {TRACKING_MODES.map(m => (
          <button
            key={m.value}
            type="button"
            disabled={loading}
            onClick={() => select(m.value)}
            className={`block w-full rounded-lg border p-3 text-left transition-colors ${
              mode === m.value
                ? "border-blue-300 bg-blue-50"
                : "border-gray-200 bg-white hover:bg-gray-50"
            }`}
          >
            <p className={`text-sm font-medium ${mode === m.value ? "text-blue-700" : "text-gray-700"}`}>
              {m.label}
            </p>
            <p className="mt-0.5 text-xs text-gray-500">{m.description}</p>
          </button>
        ))}
      </div>

      {mode === "filter" && !sources.gmailConnected && (
        <div className="mt-3 flex items-center gap-3">
          <a
            href={gmailOAuthStartUrl()}
            className="inline-block rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700"
          >
            Connect Gmail
          </a>
          <span className="text-sm text-gray-500">Needed to install per-company filters.</span>
        </div>
      )}
      {mode === "full" && !sources.gmailReadonlyConnected && (
        <div className="mt-3 flex items-center gap-3">
          <a
            href={gmailOAuthStartUrl("full")}
            className="inline-block rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700"
          >
            Grant full inbox access
          </a>
          <span className="text-sm text-gray-500">Redirects to Google's consent screen.</span>
        </div>
      )}
      {mode === "full" && sources.gmailReadonlyConnected && (
        <p className="mt-3 text-sm text-emerald-600">✓ Connected, tracking automatically.</p>
      )}
    </div>
  );
}

export function SourcesPage() {
  const { data, loading: loadingSources } = useSources();
  const [selected, setSelected] = useState<string[]>([]);
  const [saved, setSaved] = useState(false);
  const { execute, loading: saving, error } = useUpdateSources();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();

  useEffect(() => {
    if (data) setSelected(data.enabled);
  }, [data]);

  function toggle(key: string) {
    setSelected(s => (s.includes(key) ? s.filter(k => k !== key) : [...s, key]));
    setSaved(false);
  }

  async function handleSave() {
    const result = await execute(selected);
    setSelected(result.enabled);
    setSaved(true);
    // Nothing further needed if no alert-based source was chosen — move on to the
    // dashboard. Otherwise stay here so the Connect Gmail button below stays reachable.
    // alertKeys is declared below but already assigned by the time this ever runs — it's
    // only invoked from the onClick binding, after the render that declares it.
    if (!result.enabled.some(k => alertKeys.has(k))) navigate("/");
  }

  if (loadingSources) return <div className="py-12 text-center text-sm text-gray-400">Loading…</div>;

  const automatic = data?.catalog.filter(c => c.automatic) ?? [];
  const alertBased = data?.catalog.filter(c => !c.automatic) ?? [];
  const alertKeys = new Set(alertBased.map(s => s.key));
  const needsGmail = selected.some(k => alertKeys.has(k));
  const gmailStatus = searchParams.get("gmail");

  return (
    <div className="space-y-6">
      <h2 className="text-lg font-semibold text-gray-700">Choose your sources</h2>
      <p className="text-sm text-gray-500">
        Pick where job postings should come from. Automatic sources need nothing from you.
        Alert-based sources need a job alert set up on that platform, forwarded in once you
        connect Gmail. That's the next step.
      </p>

      {gmailStatus === "connected" && (
        <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-700">
          Gmail connected.
        </div>
      )}
      {gmailStatus === "error" && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          Couldn't connect Gmail. Please try again.
        </div>
      )}

      <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
        <label className={LABEL}>
          Automatic
          <InfoTooltip text="Runs on its own, nothing to set up. We search these directly for postings matching your criteria." />
        </label>
        <div className="flex flex-wrap gap-2">
          {automatic.map(s => (
            <SourceToggle key={s.key} label={s.label} active={selected.includes(s.key)} onClick={() => toggle(s.key)} />
          ))}
        </div>
      </div>

      <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
        <label className={LABEL}>
          Alert-based, needs setup
          <InfoTooltip text="You need a saved job alert already set up on that platform. Forward its emails to us via Gmail (next step) and we'll extract the postings from it." />
        </label>
        <div className="flex flex-wrap gap-2">
          {alertBased.map(s => (
            <SourceToggle key={s.key} label={s.label} active={selected.includes(s.key)} onClick={() => toggle(s.key)} />
          ))}
        </div>
      </div>

      {needsGmail && data?.gmailConnected && <GmailForwardingSetup />}

      {needsGmail && !data?.gmailConnected && (
        <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
          <label className={LABEL}>Connect Gmail</label>
          <p className="mb-3 text-sm text-gray-500">
            Lets the app manage a filter that forwards matching job alerts to us. It can only
            manage filters and settings, never read your mail.
          </p>
          <a
            href={gmailOAuthStartUrl()}
            className="inline-block rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700"
          >
            Connect Gmail
          </a>
        </div>
      )}

      {data && <GmailTrackingModeSection sources={data} />}

      <div className="flex items-center gap-3">
        <button
          onClick={handleSave}
          disabled={saving}
          className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:bg-gray-300"
        >
          {saving ? "Saving…" : "Save sources"}
        </button>
        {saved && <span className="text-sm text-emerald-600">Saved.</span>}
      </div>

      {error && <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">{error}</div>}
    </div>
  );
}
