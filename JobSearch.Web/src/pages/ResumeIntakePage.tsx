import { useState } from "react";
import { useParseResumePdf, useUpdateProfile, useUploadResumePdf } from "../hooks/useProfile";
import { BackgroundEditor } from "../components/BackgroundEditor";
import { ResumePdfViewer } from "../components/ResumePdfViewer";
import { parseBackgroundYaml, serializeBackgroundYaml, getMissingBackgroundFields, type BackgroundParseResult } from "../lib/backgroundYaml";
import { PageTagline } from "../components/PageTagline";
import { PRIMARY_BUTTON } from "../lib/styles";

// Offered by the "build from scratch" entry point below, as a starting point for someone with
// no resume to upload — BackgroundEditor's cards/add()/remove() machinery already works fine on
// an empty result, it just needs one to start from instead of requiring a parse first.
const EMPTY_BACKGROUND: BackgroundParseResult = {
  ok: true,
  data: { personal: { name: "", email: "" }, experience: [], education: [], skills: {}, projects: [], extra: {} },
};

export function ResumeIntakePage({ hideHeader = false, onboarding = false }: { hideHeader?: boolean; onboarding?: boolean } = {}) {
  const [file, setFile] = useState<File | null>(null);
  const [background, setBackground] = useState<BackgroundParseResult | null>(null);
  const [cvBase, setCvBase] = useState("");

  const parsePdf = useParseResumePdf();
  const save = useUpdateProfile();
  const uploadPdf = useUploadResumePdf();

  const error = parsePdf.error ?? save.error ?? uploadPdf.error;
  // Only the structured (parsed-ok) shape has fields to validate — the raw-text fallback (a
  // pre-existing background that doesn't parse as YAML) has nothing to check against without
  // parsing it, same as JobCriteriaEditor's Advanced raw box isn't field-validated either.
  const missing = background?.ok ? getMissingBackgroundFields(background.data) : [];

  async function handleParse() {
    const result = await parsePdf.execute(file!);
    setBackground(parseBackgroundYaml(result.background));
    setCvBase(result.cvBase);
  }

  async function handleSave() {
    if (!background || missing.length > 0) return;
    const backgroundYaml = background.ok ? serializeBackgroundYaml(background.data) : background.rawText;
    // Both land together — see the comment on POST /profile/resume-pdf for why the PDF isn't
    // sent until now rather than at parse time. When there's no file (build-from-scratch path),
    // cvBase is omitted entirely rather than sent as "" — PUT /profile routes an empty-but-
    // present CvBase through the ResumeBackfillAgent LLM reconciliation path, which only makes
    // sense against a real free-text document; omitting it lets the deterministic default
    // seeding path run instead.
    await Promise.all([
      save.execute(file ? { background: backgroundYaml, cvBase } : { background: backgroundYaml }),
      ...(file ? [uploadPdf.execute(file)] : []),
    ]);
    // Build-from-scratch (no file) during onboarding detours through the Resume Builder to pick
    // an industry template/structure before continuing to Job Criteria, instead of skipping
    // straight there. Uploading a real resume keeps today's behavior, and so does /profile's
    // usage (onboarding=false there) for an already-onboarded user.
    // Hard navigation, not client-side — useMe() only fetches /auth/me once on mount, so
    // needsOnboarding/needsCriteria need a fresh page load to reflect what was just saved.
    // A client-side navigate() would immediately bounce back here via the stale flag.
    window.location.href = onboarding && !file ? "/resume-builder?onboarding=1" : "/";
  }

  return (
    <div className="space-y-6">
      {!hideHeader && (
        <>
          <h2 className="text-lg font-semibold text-gray-700 dark:text-gray-200">Resume &amp; background</h2>
          <PageTagline>The foundation everything else gets tailored from. Worth getting right.</PageTagline>
        </>
      )}

      {!background && (
        <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-800 dark:bg-gray-900">
          <div className="flex items-center gap-3">
            <label className={`inline-block cursor-pointer ${PRIMARY_BUTTON}`}>
              Choose file
              <input
                type="file"
                accept="application/pdf"
                onChange={e => setFile(e.target.files?.[0] ?? null)}
                className="hidden"
              />
            </label>
            {file && <span className="text-sm text-gray-600 dark:text-gray-300">{file.name}</span>}
          </div>

          <button onClick={handleParse} disabled={!file || parsePdf.loading} className={`mt-4 ${PRIMARY_BUTTON}`}>
            {parsePdf.loading ? "Parsing…" : "Parse"}
          </button>

          <div className="mt-4 border-t border-gray-100 pt-4 dark:border-gray-800">
            <button
              onClick={() => setBackground(EMPTY_BACKGROUND)}
              className="text-sm font-medium text-violet-600 transition-colors hover:text-violet-700 dark:text-violet-400 dark:hover:text-violet-300"
            >
              Don't have a resume? Build one from scratch.
            </button>
          </div>
        </div>
      )}

      {background && (
        <div className="animate-fade-in-up space-y-4">
          <div>
            <p className="mb-2 text-sm font-medium text-gray-700 dark:text-gray-200">Background: review and edit before saving</p>
            <BackgroundEditor value={background} onChange={setBackground} />
          </div>

          {file && (
            <div>
              <p className="mb-2 text-sm font-medium text-gray-700 dark:text-gray-200">Base CV</p>
              <ResumePdfViewer source={file} />
            </div>
          )}

          {missing.length > 0 && (
            <div className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800 dark:border-amber-900/50 dark:bg-amber-500/10 dark:text-amber-300">
              Still needed before you can save: {missing.join(", ")}.
            </div>
          )}

          <div className="flex items-center gap-3">
            <button onClick={handleSave} disabled={save.loading || uploadPdf.loading || missing.length > 0} className={PRIMARY_BUTTON}>
              {save.loading || uploadPdf.loading ? "Saving…" : "Save to profile"}
            </button>
            <button
              onClick={() => setBackground(null)}
              className="rounded-lg px-3 py-2 text-sm font-medium text-gray-500 transition-colors hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-800"
            >
              Start over
            </button>
          </div>
        </div>
      )}

      {error && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-950/30 dark:text-red-400">{error}</div>
      )}
    </div>
  );
}
