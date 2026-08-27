import { useState, type ReactNode } from "react";
import { useGenerateCv, useGenerateLetter, useAskQuestion, useSearchPostingCandidates } from "../hooks/useGeneration";
import type { GenerationResult, PostingCandidate } from "../types";
import { PageTagline } from "../components/PageTagline";
import { GeneratingIndicator } from "../components/GeneratingIndicator";
import { AccuracyWarningBanner, CvResult, LetterResult, RevisionBox } from "../components/GenerationResult";
import { CARD, PRIMARY_BUTTON, INPUT, SECONDARY_BUTTON } from "../lib/styles";

type Mode = "url" | "text";

function PasteInsteadLink({ label, onClick }: { label: string; onClick: () => void }) {
  return (
    <button onClick={onClick} className="text-xs font-medium text-violet-600 transition-colors hover:text-violet-700 dark:text-violet-400 dark:hover:text-violet-300">
      {label}
    </button>
  );
}

function TabButton({ active, onClick, children }: { active: boolean; onClick: () => void; children: ReactNode }) {
  return (
    <button
      onClick={onClick}
      className={`rounded-lg px-3 py-1.5 text-sm font-medium transition-colors duration-150 ${
        active
          ? "bg-violet-50 text-violet-700 dark:bg-violet-500/15 dark:text-violet-300"
          : "text-gray-500 hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-800"
      }`}
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
  const [question, setQuestion] = useState("");
  const [cvResult, setCvResult] = useState<GenerationResult | null>(null);
  const [letterResult, setLetterResult] = useState<GenerationResult | null>(null);
  const [answerResult, setAnswerResult] = useState<GenerationResult | null>(null);
  const [candidates, setCandidates] = useState<PostingCandidate[] | null>(null);

  const generateCv = useGenerateCv();
  const generateLetter = useGenerateLetter();
  const askQuestion = useAskQuestion();
  const searchCandidates = useSearchPostingCandidates();

  const postingInput = mode === "url"
    ? { postingUrl, postingTitle: postingTitle || undefined, postingCompany: postingCompany || undefined }
    : { postingText };
  const canSubmitPosting = mode === "url" ? postingUrl.trim().length > 0 : postingText.trim().length > 0;
  const error = generateCv.error ?? generateLetter.error ?? askQuestion.error ?? searchCandidates.error;
  // A URL fetch failure is the only case title/company help with — reveal the fields once
  // that's happened rather than cluttering the common case where the link just works.
  const showHintFields = mode === "url" && (error !== null || postingTitle.length > 0);

  async function handleSearchCandidates() {
    setCandidates((await searchCandidates.execute(postingTitle, postingCompany || undefined)).candidates);
  }

  // Uses the candidate's own PostingText (built from the search result itself) rather than
  // re-fetching candidate.url — a site that blocked the original link will just as readily
  // block this URL too, even though it's the same listing. Switching to text mode makes that
  // switch visible rather than silently sending different content than the "Paste URL" tab
  // still shows.
  function handlePickCandidate(candidate: PostingCandidate) {
    setPostingText(candidate.postingText);
    setMode("text");
    setCandidates(null);
  }

  async function handleGenerateCv() {
    setCvResult(await generateCv.execute(postingInput));
  }

  async function handleGenerateLetter() {
    setLetterResult(await generateLetter.execute(postingInput));
  }

  async function handleAskQuestion() {
    setAnswerResult(await askQuestion.execute({
      question,
      postingUrl: mode === "url" ? postingUrl : undefined,
      postingTitle: mode === "url" ? (postingTitle || undefined) : undefined,
      postingCompany: mode === "url" ? (postingCompany || undefined) : undefined,
    }));
  }

  return (
    <div className="space-y-6">
      <h2 className="text-lg font-semibold text-gray-700 dark:text-gray-200">Generate</h2>
      <PageTagline>Paste a posting, get a tailored CV, cover letter, or answer back in seconds.</PageTagline>

      <div className={CARD}>
        <div className="mb-4 flex gap-1">
          <TabButton active={mode === "url"} onClick={() => setMode("url")}>Paste URL</TabButton>
          <TabButton active={mode === "text"} onClick={() => setMode("text")}>Paste description</TabButton>
        </div>

        {mode === "url" ? (
          <div className="space-y-2">
            <div className="relative">
              <input
                value={postingUrl}
                onChange={e => setPostingUrl(e.target.value)}
                placeholder="https://…"
                className={`${INPUT} ${postingUrl.length > 0 ? "pr-8" : ""}`}
              />
              {postingUrl.length > 0 && (
                <button
                  type="button"
                  onClick={() => setPostingUrl("")}
                  aria-label="Clear URL"
                  className="absolute right-1.5 top-1/2 -translate-y-1/2 rounded-md p-1 text-gray-400 transition-colors hover:bg-gray-100 hover:text-gray-600 dark:hover:bg-gray-700 dark:hover:text-gray-300"
                >
                  ✕
                </button>
              )}
            </div>
            {showHintFields && (
              <div className="space-y-2 rounded-lg border border-gray-100 bg-gray-50 p-3 dark:border-gray-800 dark:bg-gray-800/50">
                <p className="text-xs text-gray-500 dark:text-gray-400">Couldn't fetch that link directly. Search for it instead.</p>
                <div className="flex flex-col gap-2 sm:flex-row">
                  <input
                    value={postingTitle}
                    onChange={e => { setPostingTitle(e.target.value); setCandidates(null); }}
                    placeholder="Job title (required)"
                    className={`flex-1 ${INPUT}`}
                  />
                  <input
                    value={postingCompany}
                    onChange={e => { setPostingCompany(e.target.value); setCandidates(null); }}
                    placeholder="Company (optional)"
                    className={`flex-1 ${INPUT}`}
                  />
                  <button
                    onClick={handleSearchCandidates}
                    disabled={postingTitle.trim().length === 0 || searchCandidates.loading}
                    className={`shrink-0 ${SECONDARY_BUTTON}`}
                  >
                    {searchCandidates.loading ? "Searching…" : "Search Jora/Adzuna"}
                  </button>
                </div>
              </div>
            )}

            {candidates && (
              candidates.length === 0 ? (
                <div className="space-y-1.5">
                  <p className="text-xs text-gray-400 dark:text-gray-500">No results for that search.</p>
                  <PasteInsteadLink label="Paste the job description instead →" onClick={() => setMode("text")} />
                </div>
              ) : (
                <div className="space-y-1.5">
                  <p className="text-xs text-gray-400 dark:text-gray-500">Pick the one you meant.</p>
                  {candidates.map(c => (
                    <button
                      key={c.url}
                      onClick={() => handlePickCandidate(c)}
                      className="flex w-full items-center justify-between rounded-lg border border-gray-200 px-3 py-2 text-left text-sm transition-colors duration-150 hover:border-violet-300 hover:bg-violet-50 dark:border-gray-700 dark:hover:border-violet-700 dark:hover:bg-violet-500/10"
                    >
                      <span className="min-w-0">
                        <span className="block truncate font-medium text-gray-800 dark:text-gray-100">{c.title}</span>
                        <span className="block truncate text-xs text-gray-500 dark:text-gray-400">{c.company} · {c.location}</span>
                      </span>
                      <span className="ml-2 shrink-0 rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-500 dark:bg-gray-800 dark:text-gray-400">{c.source}</span>
                    </button>
                  ))}
                  <PasteInsteadLink label="None of these? Paste the job description instead →" onClick={() => setMode("text")} />
                </div>
              )
            )}
          </div>
        ) : (
          <textarea
            value={postingText}
            onChange={e => setPostingText(e.target.value)}
            placeholder="Paste the job description here…"
            rows={8}
            className={`${INPUT} p-3`}
          />
        )}

        <div className="mt-4 flex flex-wrap gap-3">
          <button onClick={handleGenerateCv} disabled={!canSubmitPosting || generateCv.loading} className={PRIMARY_BUTTON}>
            {generateCv.loading ? "Generating…" : "Generate CV"}
          </button>
          <button onClick={handleGenerateLetter} disabled={!canSubmitPosting || generateLetter.loading} className={PRIMARY_BUTTON}>
            {generateLetter.loading ? "Generating…" : "Generate cover letter"}
          </button>
        </div>
      </div>

      {generateCv.loading && <GeneratingIndicator kind="cv" />}
      {/* Discard the previous CV the moment a regenerate starts, rather than leaving a stale,
          fully-interactive result (download link, revision box) on screen next to the
          "generating" indicator for the whole duration of the new call — that's what read as
          "two cards" even though cvResult itself is always a single value that gets replaced,
          never appended to. */}
      {cvResult && !generateCv.loading && (
        <div className={`${CARD} animate-fade-in-up`}>
          <p className="mb-2 text-sm font-medium text-gray-700 dark:text-gray-200">CV ready</p>
          <CvResult result={cvResult} onRevised={setCvResult} />
        </div>
      )}

      {generateLetter.loading && <GeneratingIndicator kind="letter" />}
      {/* Same reasoning as cvResult above. */}
      {letterResult && !generateLetter.loading && (
        <div className={`${CARD} animate-fade-in-up`}>
          <p className="mb-2 text-sm font-medium text-gray-700 dark:text-gray-200">Cover letter</p>
          <LetterResult result={letterResult} onRevised={setLetterResult} />
        </div>
      )}

      <div className={CARD}>
        <label className="mb-2 block text-sm font-medium text-gray-700 dark:text-gray-200">Ask a question about this application</label>
        <div className="flex gap-3">
          <input
            value={question}
            onChange={e => setQuestion(e.target.value)}
            placeholder="Why do you want to work here?"
            className={`flex-1 ${INPUT}`}
          />
          <button onClick={handleAskQuestion} disabled={question.trim().length === 0 || askQuestion.loading} className={PRIMARY_BUTTON}>
            {askQuestion.loading ? "Asking…" : "Ask"}
          </button>
        </div>
        {answerResult && (
          <div className="mt-4 animate-fade-in-up rounded-lg bg-gray-50 p-3 text-sm text-gray-700 dark:bg-gray-800/50 dark:text-gray-300">
            {answerResult.mode === "ask_followup" && (
              <p className="mb-1 text-xs font-medium text-amber-600 dark:text-amber-400">Needs more context:</p>
            )}
            <AccuracyWarningBanner warnings={answerResult.accuracyWarnings} />
            <p className="whitespace-pre-wrap">{answerResult.content}</p>
            <RevisionBox
              threadId={answerResult.threadId}
              placeholder={answerResult.mode === "ask_followup" ? "Your answer to the question above" : "Request changes"}
              onRevised={setAnswerResult}
            />
          </div>
        )}
      </div>

      {error && (
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-950/30 dark:text-red-400">{error}</div>
      )}
    </div>
  );
}
