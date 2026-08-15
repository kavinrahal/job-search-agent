import { useState, type ReactNode } from "react";
import { threadPdfUrl, threadDocxUrl } from "../api";
import { useGenerateCv, useGenerateLetter, useAskQuestion, useEditThread, useSearchPostingCandidates } from "../hooks/useGeneration";
import type { GenerationResult, PostingCandidate } from "../types";

type Mode = "url" | "text";

function TabButton({ active, onClick, children }: { active: boolean; onClick: () => void; children: ReactNode }) {
  return (
    <button
      onClick={onClick}
      className={`rounded-lg px-3 py-1.5 text-sm font-medium transition-colors ${
        active ? "bg-blue-50 text-blue-700" : "text-gray-500 hover:bg-gray-100"
      }`}
    >
      {children}
    </button>
  );
}

// Same input drives two things depending on the thread's state (the backend endpoint
// handles both transparently): replying to an answer-agent follow-up question, or
// requesting a revision to an already-complete CV/letter/answer.
function RevisionBox({ threadId, placeholder, onRevised }: {
  threadId: number; placeholder: string; onRevised: (r: GenerationResult) => void;
}) {
  const [message, setMessage] = useState("");
  const { execute, loading, error } = useEditThread();

  async function handleSubmit() {
    onRevised(await execute(threadId, message));
    setMessage("");
  }

  return (
    <div className="mt-3">
      <div className="flex gap-2">
        <input
          value={message}
          onChange={e => setMessage(e.target.value)}
          placeholder={placeholder}
          className="flex-1 rounded-lg border border-gray-200 p-2 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-blue-300"
        />
        <button
          onClick={handleSubmit}
          disabled={message.trim().length === 0 || loading}
          className="rounded-lg bg-gray-100 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-200 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {loading ? "Sending…" : "Send"}
        </button>
      </div>
      {error && <p className="mt-1 text-xs text-red-600">{error}</p>}
    </div>
  );
}

export function GeneratePage() {
  const [mode, setMode] = useState<Mode>("url");
  const [postingUrl, setPostingUrl] = useState("");
  const [postingText, setPostingText] = useState("");
  const [postingHint, setPostingHint] = useState("");
  const [question, setQuestion] = useState("");
  const [cvResult, setCvResult] = useState<GenerationResult | null>(null);
  const [letterResult, setLetterResult] = useState<GenerationResult | null>(null);
  const [answerResult, setAnswerResult] = useState<GenerationResult | null>(null);
  const [candidates, setCandidates] = useState<PostingCandidate[] | null>(null);

  const generateCv = useGenerateCv();
  const generateLetter = useGenerateLetter();
  const askQuestion = useAskQuestion();
  const searchCandidates = useSearchPostingCandidates();

  const postingInput = mode === "url" ? { postingUrl, postingHint: postingHint || undefined } : { postingText };
  const canSubmitPosting = mode === "url" ? postingUrl.trim().length > 0 : postingText.trim().length > 0;
  const error = generateCv.error ?? generateLetter.error ?? askQuestion.error ?? searchCandidates.error;
  // A URL fetch failure is the only case a hint helps with — reveal the field once that's
  // happened rather than cluttering the common case where the link just works.
  const showHintField = mode === "url" && (error !== null || postingHint.length > 0);

  async function handleSearchCandidates() {
    setCandidates((await searchCandidates.execute(postingHint)).candidates);
  }

  function handlePickCandidate(candidate: PostingCandidate) {
    setPostingUrl(candidate.url);
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
      postingHint: mode === "url" ? (postingHint || undefined) : undefined,
    }));
  }

  return (
    <div className="space-y-6">
      <h2 className="text-lg font-semibold text-gray-700">Generate</h2>

      <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
        <div className="mb-4 flex gap-1">
          <TabButton active={mode === "url"} onClick={() => setMode("url")}>Paste URL</TabButton>
          <TabButton active={mode === "text"} onClick={() => setMode("text")}>Paste description</TabButton>
        </div>

        {mode === "url" ? (
          <div className="space-y-2">
            <input
              value={postingUrl}
              onChange={e => setPostingUrl(e.target.value)}
              placeholder="https://…"
              className="w-full rounded-lg border border-gray-200 p-2 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-blue-300"
            />
            {showHintField && (
              <div className="flex gap-2">
                <input
                  value={postingHint}
                  onChange={e => { setPostingHint(e.target.value); setCandidates(null); }}
                  placeholder="Job title or company — helps us find it elsewhere if the link can't be fetched directly (e.g. Seek)"
                  className="flex-1 rounded-lg border border-gray-200 p-2 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-blue-300"
                />
                <button
                  onClick={handleSearchCandidates}
                  disabled={postingHint.trim().length === 0 || searchCandidates.loading}
                  className="shrink-0 rounded-lg bg-gray-100 px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-200 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  {searchCandidates.loading ? "Searching…" : "Search Jora/Adzuna"}
                </button>
              </div>
            )}

            {candidates && (
              candidates.length === 0 ? (
                <p className="text-xs text-gray-400">No results for that search.</p>
              ) : (
                <div className="space-y-1.5">
                  <p className="text-xs text-gray-400">Pick the one you meant — it'll be used as the posting URL.</p>
                  {candidates.map(c => (
                    <button
                      key={c.url}
                      onClick={() => handlePickCandidate(c)}
                      className="flex w-full items-center justify-between rounded-lg border border-gray-200 px-3 py-2 text-left text-sm hover:border-blue-300 hover:bg-blue-50"
                    >
                      <span className="min-w-0">
                        <span className="block truncate font-medium text-gray-800">{c.title}</span>
                        <span className="block truncate text-xs text-gray-500">{c.company} · {c.location}</span>
                      </span>
                      <span className="ml-2 shrink-0 rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-500">{c.source}</span>
                    </button>
                  ))}
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
            className="w-full rounded-lg border border-gray-200 p-3 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-blue-300"
          />
        )}

        <div className="mt-4 flex flex-wrap gap-3">
          <button
            onClick={handleGenerateCv}
            disabled={!canSubmitPosting || generateCv.loading}
            className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:bg-gray-300"
          >
            {generateCv.loading ? "Generating…" : "Generate CV"}
          </button>
          <button
            onClick={handleGenerateLetter}
            disabled={!canSubmitPosting || generateLetter.loading}
            className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:bg-gray-300"
          >
            {generateLetter.loading ? "Generating…" : "Generate cover letter"}
          </button>
        </div>
      </div>

      {cvResult && (
        <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
          <p className="mb-2 text-sm font-medium text-gray-700">CV ready</p>
          <a
            href={threadPdfUrl(cvResult.threadId)}
            className="inline-block rounded-lg bg-gray-100 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-200"
          >
            Download PDF
          </a>
          <RevisionBox
            threadId={cvResult.threadId}
            placeholder="Request changes (e.g. mention Docker experience)"
            onRevised={setCvResult}
          />
        </div>
      )}

      {letterResult && (
        <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
          <p className="mb-2 text-sm font-medium text-gray-700">Cover letter</p>
          <pre className="whitespace-pre-wrap font-sans text-sm text-gray-700">{letterResult.text}</pre>
          <div className="mt-3 flex gap-2">
            <a
              href={threadPdfUrl(letterResult.threadId)}
              className="inline-block rounded-lg bg-gray-100 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-200"
            >
              Download PDF
            </a>
            <a
              href={threadDocxUrl(letterResult.threadId)}
              className="inline-block rounded-lg bg-gray-100 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-200"
            >
              Download Word
            </a>
          </div>
          <RevisionBox
            threadId={letterResult.threadId}
            placeholder="Request changes"
            onRevised={setLetterResult}
          />
        </div>
      )}

      <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
        <label className="mb-2 block text-sm font-medium text-gray-700">Ask a question about this application</label>
        <div className="flex gap-3">
          <input
            value={question}
            onChange={e => setQuestion(e.target.value)}
            placeholder="Why do you want to work here?"
            className="flex-1 rounded-lg border border-gray-200 p-2 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-blue-300"
          />
          <button
            onClick={handleAskQuestion}
            disabled={question.trim().length === 0 || askQuestion.loading}
            className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:bg-gray-300"
          >
            {askQuestion.loading ? "Asking…" : "Ask"}
          </button>
        </div>
        {answerResult && (
          <div className="mt-4 rounded-lg bg-gray-50 p-3 text-sm text-gray-700">
            {answerResult.mode === "ask_followup" && (
              <p className="mb-1 text-xs font-medium text-amber-600">Needs more context:</p>
            )}
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
        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700">{error}</div>
      )}
    </div>
  );
}
