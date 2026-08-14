import { useState } from "react";
import { parseResumePdf, parseResumeText, updateProfile } from "../api";
import type { ParsedResume } from "../types";

type Mode = "text" | "pdf";

export function ResumeIntakePage() {
  const [mode, setMode] = useState<Mode>("text");
  const [resumeText, setResumeText] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [parsed, setParsed] = useState<ParsedResume | null>(null);
  const [background, setBackground] = useState("");
  const [cvBase, setCvBase] = useState("");
  const [parsing, setParsing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleParse() {
    setParsing(true);
    setError(null);
    setSaved(false);
    try {
      const result = mode === "text" ? await parseResumeText(resumeText) : await parseResumePdf(file!);
      setParsed(result);
      setBackground(result.background);
      setCvBase(result.cvBase);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to parse resume");
    } finally {
      setParsing(false);
    }
  }

  async function handleSave() {
    setSaving(true);
    setError(null);
    try {
      await updateProfile({ background, cvBase });
      setSaved(true);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to save");
    } finally {
      setSaving(false);
    }
  }

  const canParse = mode === "text" ? resumeText.trim().length > 0 : file !== null;

  return (
    <div className="space-y-6">
      <h2 className="text-lg font-semibold text-gray-700">Resume &amp; background</h2>

      {!parsed && (
        <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
          <div className="mb-4 flex gap-1">
            <button
              onClick={() => setMode("text")}
              className={`rounded-lg px-3 py-1.5 text-sm font-medium transition-colors ${
                mode === "text" ? "bg-blue-50 text-blue-700" : "text-gray-500 hover:bg-gray-100"
              }`}
            >
              Paste text
            </button>
            <button
              onClick={() => setMode("pdf")}
              className={`rounded-lg px-3 py-1.5 text-sm font-medium transition-colors ${
                mode === "pdf" ? "bg-blue-50 text-blue-700" : "text-gray-500 hover:bg-gray-100"
              }`}
            >
              Upload PDF
            </button>
          </div>

          {mode === "text" ? (
            <textarea
              value={resumeText}
              onChange={e => setResumeText(e.target.value)}
              placeholder="Paste your resume text here…"
              rows={12}
              className="w-full rounded-lg border border-gray-200 p-3 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-blue-300"
            />
          ) : (
            <input
              type="file"
              accept="application/pdf"
              onChange={e => setFile(e.target.files?.[0] ?? null)}
              className="block w-full text-sm text-gray-600"
            />
          )}

          <button
            onClick={handleParse}
            disabled={!canParse || parsing}
            className="mt-4 rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:bg-gray-300"
          >
            {parsing ? "Parsing…" : "Parse with Claude"}
          </button>
        </div>
      )}

      {parsed && (
        <div className="space-y-4">
          <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
            <label className="mb-2 block text-sm font-medium text-gray-700">
              Background — review and edit before saving
            </label>
            <textarea
              value={background}
              onChange={e => setBackground(e.target.value)}
              rows={16}
              className="w-full rounded-lg border border-gray-200 p-3 font-mono text-xs text-gray-700 focus:outline-none focus:ring-2 focus:ring-blue-300"
            />
          </div>

          <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
            <label className="mb-2 block text-sm font-medium text-gray-700">
              Base CV — review and edit before saving
            </label>
            <textarea
              value={cvBase}
              onChange={e => setCvBase(e.target.value)}
              rows={16}
              className="w-full rounded-lg border border-gray-200 p-3 font-mono text-xs text-gray-700 focus:outline-none focus:ring-2 focus:ring-blue-300"
            />
          </div>

          <div className="flex items-center gap-3">
            <button
              onClick={handleSave}
              disabled={saving}
              className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:bg-gray-300"
            >
              {saving ? "Saving…" : "Save to profile"}
            </button>
            <button
              onClick={() => { setParsed(null); setSaved(false); }}
              className="rounded-lg px-3 py-2 text-sm font-medium text-gray-500 hover:bg-gray-100"
            >
              Start over
            </button>
            {saved && <span className="text-sm text-emerald-600">Saved.</span>}
          </div>
        </div>
      )}

      {error && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">{error}</div>
      )}
    </div>
  );
}
