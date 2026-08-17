import { useEffect, useState } from "react";
import { useSearchParams, useNavigate } from "react-router-dom";
import { useSources, useUpdateSources } from "../hooks/useSources";
import { gmailOAuthStartUrl } from "../api";

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
        connect Gmail — that's the next step.
      </p>

      {gmailStatus === "connected" && (
        <div className="rounded-xl border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-700">
          Gmail connected.
        </div>
      )}
      {gmailStatus === "error" && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          Couldn't connect Gmail — please try again.
        </div>
      )}

      <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
        <label className={LABEL}>Automatic</label>
        <div className="flex flex-wrap gap-2">
          {automatic.map(s => (
            <SourceToggle key={s.key} label={s.label} active={selected.includes(s.key)} onClick={() => toggle(s.key)} />
          ))}
        </div>
      </div>

      <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
        <label className={LABEL}>Alert-based — needs setup</label>
        <div className="flex flex-wrap gap-2">
          {alertBased.map(s => (
            <SourceToggle key={s.key} label={s.label} active={selected.includes(s.key)} onClick={() => toggle(s.key)} />
          ))}
        </div>
      </div>

      {needsGmail && (
        <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
          <label className={LABEL}>Connect Gmail</label>
          <p className="mb-3 text-sm text-gray-500">
            Lets the app manage a filter that forwards matching job alerts to us — it can only
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
