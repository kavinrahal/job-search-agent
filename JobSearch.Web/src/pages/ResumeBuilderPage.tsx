import { useEffect, useState } from "react";
import { useResume, useResumeTemplates, useUpdateResume, useApplyResumeTemplate } from "../hooks/useResume";
import { ResumeBuilder } from "../components/ResumeBuilder";
import { PageTagline } from "../components/PageTagline";
import { PRIMARY_BUTTON } from "../lib/styles";
import type { ResumeData } from "../types";

const EMPTY: ResumeData = { summary: "", sectionConfig: [], updatedAt: "" };

export function ResumeBuilderPage() {
  const { data: resume, loading: loadingResume, error: loadError, reload } = useResume();
  const { data: templates, loading: loadingTemplates } = useResumeTemplates();
  const [draft, setDraft] = useState<ResumeData>(EMPTY);
  const { execute: save, loading: saving, error: saveError } = useUpdateResume();
  const { execute: applyTemplate, loading: applying, error: applyError } = useApplyResumeTemplate();

  // Reflects whatever's already saved, same pattern as JobCriteriaPage/CriteriaWizard — editing
  // from a blank slate would otherwise silently wipe out an existing section order.
  useEffect(() => {
    if (resume) setDraft(resume);
  }, [resume]);

  async function handleApplyTemplate(industryKey: string, seniority?: "junior" | "experienced") {
    const updated = await applyTemplate(industryKey, seniority);
    setDraft(updated);
  }

  async function handleSave() {
    await save({ summary: draft.summary, sectionConfig: draft.sectionConfig });
    reload();
  }

  // loadError gets its own amber presentation below (it's most often the expected "resume
  // setup isn't finished yet" 409, not an unexpected failure) — this is only for save/apply.
  const error = saveError ?? applyError;

  if (loadingResume || loadingTemplates) {
    return <div className="py-12 text-center text-sm text-gray-400 dark:text-gray-500">Loading…</div>;
  }

  return (
    <div className="space-y-6">
      <h2 className="text-lg font-semibold text-gray-700 dark:text-gray-200">Resume builder</h2>
      <PageTagline>How your resume is laid out — pick an industry starting point, then fine-tune what's shown and in what order.</PageTagline>

      {loadError ? (
        <div className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800 dark:border-amber-900/50 dark:bg-amber-500/10 dark:text-amber-300">
          {loadError}
        </div>
      ) : (
        <>
          <ResumeBuilder
            value={draft}
            onChange={setDraft}
            industries={templates?.industries ?? []}
            onApplyTemplate={handleApplyTemplate}
            applyingTemplate={applying}
          />

          <div className="flex items-center gap-3">
            <button onClick={handleSave} disabled={saving} className={PRIMARY_BUTTON}>
              {saving ? "Saving…" : "Save resume"}
            </button>
          </div>
        </>
      )}

      {error && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-950/30 dark:text-red-400">{error}</div>
      )}
    </div>
  );
}
