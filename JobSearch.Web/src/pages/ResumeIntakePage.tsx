import { useRef, useState } from "react";
import { useParseResumePdf, useUpdateProfile, useUploadResumePdf } from "../hooks/useProfile";
import { BackgroundEditor } from "../components/BackgroundEditor";
import { ResumePdfViewer } from "../components/ResumePdfViewer";
import { parseBackgroundYaml, serializeBackgroundYaml, getMissingBackgroundFields, type BackgroundParseResult } from "../lib/backgroundYaml";
import { PageHeader, Surface, Button, Callout } from "../ui";

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
  const fileInputRef = useRef<HTMLInputElement>(null);

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
        <PageHeader title="Resume & background" tagline="The foundation everything else gets tailored from. Worth getting right." />
      )}

      {!background && (
        <Surface padding="lg">
          <div className="flex items-center gap-3">
            <input
              ref={fileInputRef}
              type="file"
              accept="application/pdf"
              onChange={e => setFile(e.target.files?.[0] ?? null)}
              className="hidden"
            />
            <Button onClick={() => fileInputRef.current?.click()}>Choose file</Button>
            {file && <span className="text-body text-ink-2">{file.name}</span>}
          </div>

          <Button className="mt-4" onClick={handleParse} disabled={!file || parsePdf.loading}>
            {parsePdf.loading ? "Parsing…" : "Parse"}
          </Button>

          <div className="hairline-t mt-4 pt-4">
            <button
              type="button"
              onClick={() => setBackground(EMPTY_BACKGROUND)}
              className="text-note font-[650] text-ember transition-colors hover:text-ember-hi focus-ring rounded-ctl"
            >
              Don't have a resume? Build one from scratch.
            </button>
          </div>
        </Surface>
      )}

      {background && (
        <div className="animate-fade-in-up space-y-4">
          <div>
            <p className="mb-2 text-body font-[650] text-ink-2">Background: review and edit before saving</p>
            <BackgroundEditor value={background} onChange={setBackground} />
          </div>

          {file && (
            <div>
              <p className="mb-2 text-body font-[650] text-ink-2">Base CV</p>
              <ResumePdfViewer source={file} />
            </div>
          )}

          {missing.length > 0 && (
            <Callout variant="warning" title={`Still needed before you can save: ${missing.join(", ")}.`} />
          )}

          <div className="flex items-center gap-3">
            <Button onClick={handleSave} disabled={save.loading || uploadPdf.loading || missing.length > 0}>
              {save.loading || uploadPdf.loading ? "Saving…" : "Save to profile"}
            </Button>
            <Button variant="ghost" onClick={() => setBackground(null)}>
              Start over
            </Button>
          </div>
        </div>
      )}

      {error && <Callout variant="danger" title={error} />}
    </div>
  );
}
