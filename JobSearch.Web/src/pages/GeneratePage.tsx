import { useEffect, useState, type ReactNode } from "react";
import { useGenerateCv, useGenerateLetter, useSearchPostingCandidates } from "../hooks/useGeneration";
import { useMeContext } from "../hooks/useMeContext";
import { fetchThread } from "../api";
import { rememberThread, recallThread, forgetThread } from "../lib/lastGeneration";
import type { GenerationResult, PostingCandidate } from "../types";
import { GeneratingIndicator } from "../components/GeneratingIndicator";
import { CvResult, LetterResult } from "../components/GenerationResult";
import { Surface, Well, Button, Callout, Input, Textarea, IconButton, CloseIcon, DocumentPage, cx } from "../ui";

type Mode = "url" | "text";

// localStorage keys for the last CV/letter generated here, so an accidental refresh restores the
// result (see lib/lastGeneration). One per artifact type — a page can hold one of each at once.
const CV_THREAD_KEY = "lastCvThreadId";
const LETTER_THREAD_KEY = "lastLetterThreadId";

function PasteInsteadLink({ label, onClick }: { label: string; onClick: () => void }) {
  return (
    <button onClick={onClick} className="text-caption font-[650] text-ember hover:text-ember-hi">
      {label}
    </button>
  );
}

// Sign in / Create account style tab switch — see LandingPage's TabSwitch for the same
// hand-rolled treatment and why it isn't SegmentedControl (this swaps input modes, it doesn't
// filter a list already on screen).
function ModeTab({ active, onClick, children }: { active: boolean; onClick: () => void; children: ReactNode }) {
  return (
    <button
      onClick={onClick}
      className={cx(
        "rounded-inset px-3 py-1.5 text-control font-[650] tappable focus-ring",
        "transition-[background-color,color,transform] duration-350 ease-spring motion-reduce:transition-none active:scale-[.97]",
        active ? "bg-core text-ink shadow-e1" : "text-muted hover:text-ink",
      )}
    >
      {children}
    </button>
  );
}

export function GeneratePage() {
  const [mode, setMode] = useState<Mode>("url");
  const [postingUrl, setPostingUrl] = useState("");
  const [postingText, setPostingText] = useState("");
  const [postingTitle, setPostingTitle] = useState("");
  const [postingCompany, setPostingCompany] = useState("");
  const [cvResult, setCvResult] = useState<GenerationResult | null>(null);
  const [letterResult, setLetterResult] = useState<GenerationResult | null>(null);
  const [candidates, setCandidates] = useState<PostingCandidate[] | null>(null);

  const generateCv = useGenerateCv();
  const generateLetter = useGenerateLetter();
  const searchCandidates = useSearchPostingCandidates();
  const { reloadMe } = useMeContext();

  // Restore the most recent CV/letter (if generated within the last 24h) after an accidental
  // refresh, so the user lands back on their result rather than an empty form. A dropped/expired
  // thread just clears its key and leaves the form empty. Mount-only — this reacts to a fresh page
  // load, not to any changing state.
  useEffect(() => {
    const cvId = recallThread(CV_THREAD_KEY);
    if (cvId !== null) fetchThread(cvId).then(setCvResult).catch(() => forgetThread(CV_THREAD_KEY));
    const letterId = recallThread(LETTER_THREAD_KEY);
    if (letterId !== null) fetchThread(letterId).then(setLetterResult).catch(() => forgetThread(LETTER_THREAD_KEY));
  }, []);

  const postingInput = mode === "url"
    ? { postingUrl, postingTitle: postingTitle || undefined, postingCompany: postingCompany || undefined }
    : { postingText };
  const canSubmitPosting = mode === "url" ? postingUrl.trim().length > 0 : postingText.trim().length > 0;
  const error = generateCv.error ?? generateLetter.error ?? searchCandidates.error;
  // A URL fetch failure is the only case title/company help with — reveal the fields once
  // that's happened rather than cluttering the common case where the link just works.
  const showHintFields = mode === "url" && (error !== null || postingTitle.length > 0);

  async function handleSearchCandidates() {
    setCandidates((await searchCandidates.execute(postingTitle, postingCompany || undefined)).candidates);
  }

  // Uses the candidate's own PostingText (built from the search result itself) rather than
  // re-fetching candidate.url — a site that blocked the original link will just as readily
  // block this URL too, even though it's the same listing. Switching to text mode makes that
  // switch visible rather than silently sending different content than the "Paste link" tab
  // still shows.
  function handlePickCandidate(candidate: PostingCandidate) {
    setPostingText(candidate.postingText);
    setMode("text");
    setCandidates(null);
  }

  async function handleGenerateCv() {
    const result = await generateCv.execute(postingInput);
    setCvResult(result);
    rememberThread(CV_THREAD_KEY, result.threadId);
    reloadMe();
  }

  async function handleGenerateLetter() {
    const result = await generateLetter.execute(postingInput);
    setLetterResult(result);
    rememberThread(LETTER_THREAD_KEY, result.threadId);
    reloadMe();
  }

  return (
    <div className="space-y-6">
      {/* Form left, live A4 preview right — matching the approved prototype's layout (see
          ResumeBuilderPage's own form-rail + sticky-preview split for the same pattern). Stacks
          to a single column below lg, where the preview simply follows the form in source order
          rather than needing a separate mobile toggle (there is nothing to switch away from). */}
      <div className="grid grid-cols-1 items-start gap-3.5 lg:grid-cols-[.9fr_1.1fr]">
        <div className="space-y-3.5">
          <Surface elevation="raised">
            <div className="surface-sunk mb-4 inline-flex gap-px rounded-ctl p-[3px]">
              <ModeTab active={mode === "url"} onClick={() => setMode("url")}>Paste link</ModeTab>
              <ModeTab active={mode === "text"} onClick={() => setMode("text")}>Paste description</ModeTab>
            </div>

            {mode === "url" ? (
              <div className="space-y-2">
                <div className="relative">
                  <Input
                    label="Job posting link"
                    value={postingUrl}
                    onChange={e => setPostingUrl(e.target.value)}
                    placeholder="https://…"
                    className={postingUrl.length > 0 ? "pr-9" : undefined}
                  />
                  {postingUrl.length > 0 && (
                    <IconButton
                      aria-label="Clear link"
                      size="sm"
                      onClick={() => setPostingUrl("")}
                      className="absolute top-[26px] right-1.5"
                    >
                      <CloseIcon className="h-3.5 w-3.5" />
                    </IconButton>
                  )}
                </div>
                {showHintFields && (
                  <Well className="space-y-2 p-3">
                    <p className="m-0 text-caption text-muted">Couldn't fetch that link directly. Search for it instead.</p>
                    <div className="flex flex-col gap-2 sm:flex-row sm:items-end">
                      <Input
                        label="Job title"
                        className="flex-1"
                        value={postingTitle}
                        onChange={e => { setPostingTitle(e.target.value); setCandidates(null); }}
                        placeholder="Required"
                      />
                      <Input
                        label="Company"
                        className="flex-1"
                        value={postingCompany}
                        onChange={e => { setPostingCompany(e.target.value); setCandidates(null); }}
                        placeholder="Optional"
                      />
                      <Button
                        variant="subtle"
                        onClick={handleSearchCandidates}
                        disabled={postingTitle.trim().length === 0 || searchCandidates.loading}
                        className="shrink-0"
                      >
                        {searchCandidates.loading ? "Searching…" : "Search Jora/Adzuna"}
                      </Button>
                    </div>
                  </Well>
                )}

                {candidates && (
                  candidates.length === 0 ? (
                    <div className="space-y-1.5">
                      <p className="m-0 text-caption text-faint">No results for that search.</p>
                      <PasteInsteadLink label="Paste the job description instead →" onClick={() => setMode("text")} />
                    </div>
                  ) : (
                    <div className="space-y-1.5">
                      <p className="m-0 text-caption text-faint">Pick the one you meant.</p>
                      {candidates.map(c => (
                        <button
                          key={c.url}
                          onClick={() => handlePickCandidate(c)}
                          className="hairline-ring flex w-full items-center justify-between rounded-ctl px-3 py-2 text-left text-body tappable transition-colors duration-300 hover:bg-shell"
                        >
                          <span className="min-w-0">
                            <span className="block truncate font-[650] text-ink">{c.title}</span>
                            <span className="block truncate text-caption text-faint">{c.company} · {c.location}</span>
                          </span>
                          <span className="ml-2 shrink-0 rounded-pill bg-shell px-2 py-0.5 text-caption text-faint">{c.source}</span>
                        </button>
                      ))}
                      <PasteInsteadLink label="None of these? Paste the job description instead →" onClick={() => setMode("text")} />
                    </div>
                  )
                )}
              </div>
            ) : (
              <Textarea
                label="Job description"
                value={postingText}
                onChange={e => setPostingText(e.target.value)}
                placeholder="Paste the job description here…"
                rows={8}
              />
            )}

            <div className="mt-4 flex flex-wrap gap-3">
              <Button cap onClick={handleGenerateCv} disabled={!canSubmitPosting || generateCv.loading} loading={generateCv.loading}>
                {generateCv.loading ? "Generating…" : "Generate CV"}
              </Button>
              <Button variant="ghost" onClick={handleGenerateLetter} disabled={!canSubmitPosting || generateLetter.loading} loading={generateLetter.loading}>
                {generateLetter.loading ? "Generating…" : "Cover letter"}
              </Button>
            </div>
            <p className="m-0 mt-2.5 text-caption text-faint">Each generation uses 1 credit.</p>
          </Surface>

          {/* Persistent, not tied to a specific result — the same proactive reminder the
              prototype shows in the form's default state, before anything has been generated.
              A generated CV/letter's own specific accuracy warnings still surface inline with
              that result below (AccuracyWarningBanner), this is the general-purpose companion. */}
          <Callout variant="warning" title="Worth checking before you send.">
            Always double-check names, dates, and specific claims before you send anything generated.
          </Callout>
        </div>

        {/* The live CV preview. Mirrors ResumePreviewPane/ResumePdfViewer's own "fixed A4 page,
            never reflows" convention (DocumentPage, ui) — empty until a CV exists for this
            posting, then the real generated document, kept in sync through regenerates and
            revisions. */}
        <div className="lg:sticky lg:top-4">
          {generateCv.loading ? (
            <GeneratingIndicator kind="cv" />
          ) : cvResult ? (
            <Surface elevation="raised" className="animate-fade-in-up">
              <p className="m-0 mb-2 text-body font-[650] text-ink-2">CV ready</p>
              <CvResult result={cvResult} onRevised={setCvResult} />
            </Surface>
          ) : (
            <DocumentPage>
              <div className="flex h-full items-center justify-center p-8 text-center">
                <p className="m-0 text-caption text-[#8a8f99]">Your tailored CV will appear here once you generate one.</p>
              </div>
            </DocumentPage>
          )}
        </div>
      </div>

      {generateLetter.loading && <GeneratingIndicator kind="letter" />}
      {/* Discarded the moment a regenerate starts, same reasoning as the CV panel above — no
          stale, fully-interactive letter sitting next to the "generating" indicator. */}
      {letterResult && !generateLetter.loading && (
        <Surface elevation="raised" className="animate-fade-in-up">
          <p className="m-0 mb-2 text-body font-[650] text-ink-2">Cover letter</p>
          <LetterResult result={letterResult} onRevised={setLetterResult} />
        </Surface>
      )}

      {error && <Callout variant="danger" title={error} />}
    </div>
  );
}
