import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useParseResumePdf, useUpdateProfile, useUploadResumePdf } from "../hooks/useProfile";
import { BackgroundEditor } from "../components/BackgroundEditor";
import { ResumePdfViewer } from "../components/ResumePdfViewer";
import { parseBackgroundYaml, serializeBackgroundYaml, type BackgroundParseResult } from "../lib/backgroundYaml";

export function ResumeIntakePage() {
  const navigate = useNavigate();
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
    navigate("/");
  }

  return (
    <div className="space-y-6">
      <h2 className="text-lg font-semibold text-gray-700">Resume &amp; background</h2>

      {!background && (
        <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
          <div className="flex items-center gap-3">
            <label className="inline-block cursor-pointer rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700">
              Choose file
              <input
                type="file"
                accept="application/pdf"
                onChange={e => setFile(e.target.files?.[0] ?? null)}
                className="hidden"
              />
            </label>
            {file && <span className="text-sm text-gray-600">{file.name}</span>}
          </div>

          <button
            onClick={handleParse}
            disabled={!file || parsePdf.loading}
            className="mt-4 rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:bg-gray-300"
          >
            {parsePdf.loading ? "Parsing…" : "Parse"}
          </button>
        </div>
      )}

      {background && (
        <div className="space-y-4">
          <div>
            <p className="mb-2 text-sm font-medium text-gray-700">Background: review and edit before saving</p>
            <BackgroundEditor value={background} onChange={setBackground} />
          </div>

          <div>
            <p className="mb-2 text-sm font-medium text-gray-700">Base CV</p>
            {file && <ResumePdfViewer source={file} />}
          </div>

          <div className="flex items-center gap-3">
            <button
              onClick={handleSave}
              disabled={save.loading || uploadPdf.loading}
              className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:bg-gray-300"
            >
              {save.loading || uploadPdf.loading ? "Saving…" : "Save to profile"}
            </button>
            <button
              onClick={() => setBackground(null)}
              className="rounded-lg px-3 py-2 text-sm font-medium text-gray-500 hover:bg-gray-100"
            >
              Start over
            </button>
          </div>
        </div>
      )}

      {error && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">{error}</div>
      )}
    </div>
  );
}
