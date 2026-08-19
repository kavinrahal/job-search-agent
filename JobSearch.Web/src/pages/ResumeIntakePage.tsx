import { useState } from "react";
import { useParseResumePdf, useUpdateProfile, useUploadResumePdf } from "../hooks/useProfile";
import { BackgroundEditor } from "../components/BackgroundEditor";
import { ResumePdfViewer } from "../components/ResumePdfViewer";
import { parseBackgroundYaml, serializeBackgroundYaml, type BackgroundParseResult } from "../lib/backgroundYaml";
import { PageTagline } from "../components/PageTagline";
import { PRIMARY_BUTTON } from "../lib/styles";

export function ResumeIntakePage({ hideHeader = false }: { hideHeader?: boolean } = {}) {
  const [file, setFile] = useState<File | null>(null);
  const [background, setBackground] = useState<BackgroundParseResult | null>(null);
  const [cvBase, setCvBase] = useState("");

  const parsePdf = useParseResumePdf();
  const save = useUpdateProfile();
  const uploadPdf = useUploadResumePdf();

  const error = parsePdf.error ?? save.error ?? uploadPdf.error;

  async function handleParse() {
    const result = await parsePdf.execute(file!);
    setBackground(parseBackgroundYaml(result.background));
    setCvBase(result.cvBase);
  }

  async function handleSave() {
    if (!background || !file) return;
    const backgroundYaml = background.ok ? serializeBackgroundYaml(background.data) : background.rawText;
    // Both land together — see the comment on POST /profile/resume-pdf for why the PDF isn't
    // sent until now rather than at parse time.
    await Promise.all([
      save.execute({ background: backgroundYaml, cvBase }),
      uploadPdf.execute(file),
    ]);
    // Hard navigation, not client-side — useMe() only fetches /auth/me once on mount, so
    // needsOnboarding/needsCriteria need a fresh page load to reflect what was just saved.
    // A client-side navigate("/") would immediately bounce back here via the stale flag.
    window.location.href = "/";
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
        </div>
      )}

      {background && (
        <div className="animate-fade-in-up space-y-4">
          <div>
            <p className="mb-2 text-sm font-medium text-gray-700 dark:text-gray-200">Background: review and edit before saving</p>
            <BackgroundEditor value={background} onChange={setBackground} />
          </div>

          <div>
            <p className="mb-2 text-sm font-medium text-gray-700 dark:text-gray-200">Base CV</p>
            {file && <ResumePdfViewer source={file} />}
          </div>

          <div className="flex items-center gap-3">
            <button onClick={handleSave} disabled={save.loading || uploadPdf.loading} className={PRIMARY_BUTTON}>
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
