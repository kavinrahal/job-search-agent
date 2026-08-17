import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useParseResumeText, useParseResumePdf, useUpdateProfile, useUploadResumePdf } from "../hooks/useProfile";
import { BackgroundEditor } from "../components/BackgroundEditor";
import { ResumePdfViewer } from "../components/ResumePdfViewer";
import { parseBackgroundYaml, serializeBackgroundYaml, type BackgroundParseResult } from "../lib/backgroundYaml";

type Mode = "text" | "pdf";

export function ResumeIntakePage() {
  const navigate = useNavigate();
  const [mode, setMode] = useState<Mode>("text");
  const [resumeText, setResumeText] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [background, setBackground] = useState<BackgroundParseResult | null>(null);
  const [cvBase, setCvBase] = useState("");

  const parseText = useParseResumeText();
  const parsePdf = useParseResumePdf();
  const save = useUpdateProfile();
  const uploadPdf = useUploadResumePdf();

  const parsing = parseText.loading || parsePdf.loading;
  const error = parseText.error ?? parsePdf.error ?? save.error ?? uploadPdf.error;

  async function handleParse() {
    const result = mode === "text" ? await parseText.execute(resumeText) : await parsePdf.execute(file!);
    setBackground(parseBackgroundYaml(result.background));
    setCvBase(result.cvBase);
  }

  async function handleSave() {
    if (!background) return;
    const backgroundYaml = background.ok ? serializeBackgroundYaml(background.data) : background.rawText;
    // Both land together — see the comment on POST /profile/resume-pdf for why the PDF isn't
    // sent until now rather than at parse time.
    await Promise.all([
      save.execute({ background: backgroundYaml, cvBase }),
      mode === "pdf" && file ? uploadPdf.execute(file) : Promise.resolve(),
    ]);
    navigate("/");
  }

  const canParse = mode === "text" ? resumeText.trim().length > 0 : file !== null;

  return (
    <div className="space-y-6">
      <h2 className="text-lg font-semibold text-gray-700">Resume &amp; background</h2>

      {!background && (
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

      {background && (
        <div className="space-y-4">
          <div>
            <p className="mb-2 text-sm font-medium text-gray-700">Background — review and edit before saving</p>
            <BackgroundEditor value={background} onChange={setBackground} />
          </div>

          <div>
            <p className="mb-2 text-sm font-medium text-gray-700">Base CV</p>
            {mode === "pdf" && file ? (
              <ResumePdfViewer source={file} />
            ) : (
              <p className="rounded-xl border border-gray-200 bg-gray-50 p-4 text-sm text-gray-500">
                No PDF on file — your pasted text is used as the base CV. Upload a PDF instead if
                you'd rather review it visually here.
              </p>
            )}
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
