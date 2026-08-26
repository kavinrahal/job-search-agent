import { useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useMe } from "../hooks/useAuth";
import { useResume, useResumeTemplates, useUpdateResume, useApplyResumeTemplate } from "../hooks/useResume";
import { useProfile } from "../hooks/useProfile";
import { useSyncedState } from "../hooks/useSyncedState";
import { useDebouncedPreview } from "../hooks/useDebouncedPreview";
import { ResumeBuilder } from "../components/ResumeBuilder";
import { ResumePreviewPane } from "../components/ResumePreviewPane";
import { ChoiceButtons } from "../components/ChoiceButtons";
import { PageTagline } from "../components/PageTagline";
import { PRIMARY_BUTTON } from "../lib/styles";
import { parseBackgroundYaml } from "../lib/backgroundYaml";
import { applyTemplateToDraft } from "../lib/resumeSections";
import type { ResumeData } from "../types";
import type { ResumeDraft } from "../api";

const EMPTY: ResumeData = {
  summary: "", sectionConfig: [], experienceOverrides: [], projectOverrides: [], skillsSection: [], updatedAt: "",
};

// Form rail + live preview pane, matching the approved prototype's layout: the preview is a
// fixed-width "document" (see ResumePreviewPane.tsx) that represents the real PDF faithfully
// rather than reflowing per viewport, and on narrow screens an Edit/Preview toggle replaces
// stacking the two vertically — the repo owner specifically corrected an earlier prototype pass
// that shrank the document on mobile instead of scrolling it, so this stays a straight
// grid-columns-on-desktop / toggle-on-mobile split, not a responsive redesign of the document.
export function ResumeBuilderPage() {
  // Set when reached via ResumeIntakePage's build-from-scratch onboarding detour (see
  // ResumeIntakePage.handleSave) — signals that a successful save *may* continue the onboarding
  // flow rather than stay on this page as a normal persistent-editor save. Query-param presence
  // alone isn't enough to trust, though (e.g. a bookmarked/shared /resume-builder?onboarding=1
  // link visited by an already-onboarded account) — see the needsCriteria check in handleSave.
  const [searchParams] = useSearchParams();
  const onboarding = searchParams.get("onboarding") === "1";
  const { data: me, loading: loadingMe } = useMe();
  const { data: resume, loading: loadingResume, error: loadError, reload } = useResume();
  const { data: templates, loading: loadingTemplates } = useResumeTemplates();
  // Background's read-only Experience/Projects entries for the override editors — same
  // /profile + parseBackgroundYaml route BackgroundEditor.tsx already uses, no new endpoint.
  const { data: profile, loading: loadingProfile } = useProfile();
  // Reflects whatever's already saved, same pattern as JobCriteriaPage/CriteriaWizard — editing
  // from a blank slate would otherwise silently wipe out an existing section order.
  const [draft, setDraft] = useSyncedState(resume, EMPTY, r => r);
  const { execute: save, loading: saving, error: saveError } = useUpdateResume();
  const { execute: applyTemplate, loading: applying, error: applyError } = useApplyResumeTemplate();
  const [mobileView, setMobileView] = useState<"edit" | "preview">("edit");

  const loading = loadingResume || loadingTemplates || loadingProfile || loadingMe;
  const parsedBackground = profile ? parseBackgroundYaml(profile.background) : null;
  const background = parsedBackground?.ok ? parsedBackground.data : null;

  const previewDraft: ResumeDraft = {
    summary: draft.summary,
    sectionConfig: draft.sectionConfig,
    experienceOverrides: draft.experienceOverrides,
    projectOverrides: draft.projectOverrides,
    skillsSection: draft.skillsSection,
  };
  const { markdown, loading: loadingPreview, error: previewError } = useDebouncedPreview(previewDraft, !loading);

  async function handleApplyTemplate(industryKey: string, seniority?: "junior" | "experienced") {
    const updated = await applyTemplate(industryKey, seniority);
    setDraft(prev => applyTemplateToDraft(prev, updated));
  }

  async function handleSave() {
    await save(previewDraft);
    // Only continue the onboarding detour when the account is actually still mid-detour
    // (needsCriteria still true) — the ?onboarding=1 param alone isn't trustworthy, since a
    // fully-onboarded account can land here too via a bookmarked/shared link, and for them a
    // Resume Builder save should behave like any other visit (stay put, reload) rather than
    // hard-navigating away.
    if (onboarding && me?.needsCriteria) {
      // Hard navigation, same pattern ResumeIntakePage uses for the same reason — useMe()
      // only fetches /auth/me once on mount, so a fresh page load is needed for
      // needsCriteria to pick up and send the user to Job Criteria next.
      window.location.href = "/";
      return;
    }
    reload();
  }

  // loadError gets its own amber presentation below (it's most often the expected "resume
  // setup isn't finished yet" 409, not an unexpected failure) — this is only for save/apply.
  const error = saveError ?? applyError;

  if (loading) {
    return <div className="py-12 text-center text-sm text-gray-400 dark:text-gray-500">Loading…</div>;
  }

  return (
    <div className="space-y-6">
      <h2 className="text-lg font-semibold text-gray-700 dark:text-gray-200">Resume builder</h2>
      <PageTagline>How your resume is laid out and worded — pick an industry starting point, then fine-tune what's shown, the wording, and the order.</PageTagline>

      {loadError ? (
        <div className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800 dark:border-amber-900/50 dark:bg-amber-500/10 dark:text-amber-300">
          {loadError}
        </div>
      ) : (
        <>
          {/* Mobile/tablet only — the lg: grid below shows form and preview side by side, where
              this toggle would be redundant. */}
          <div className="lg:hidden">
            <ChoiceButtons
              options={[{ value: "edit" as const, label: "Edit" }, { value: "preview" as const, label: "Preview" }]}
              value={mobileView}
              onChange={setMobileView}
            />
          </div>

          <div className="grid grid-cols-1 gap-6 lg:grid-cols-[minmax(0,1fr)_520px]">
            <div className={`space-y-4 ${mobileView === "preview" ? "hidden lg:block" : ""}`}>
              <ResumeBuilder
                value={draft}
                onChange={setDraft}
                industries={templates?.industries ?? []}
                onApplyTemplate={handleApplyTemplate}
                applyingTemplate={applying}
                background={background}
              />
              <button onClick={handleSave} disabled={saving} className={PRIMARY_BUTTON}>
                {saving ? "Saving…" : "Save resume"}
              </button>
            </div>

            <div className={mobileView === "edit" ? "hidden lg:block" : ""}>
              <div className="lg:sticky lg:top-4">
                <ResumePreviewPane markdown={markdown} loading={loadingPreview} error={previewError} />
              </div>
            </div>
          </div>
        </>
      )}

      {error && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-950/30 dark:text-red-400">{error}</div>
      )}
    </div>
  );
}
