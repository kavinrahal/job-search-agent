import { useState, type ReactNode } from "react";
import { generateCv, generateLetter, askQuestion, editThread, cvPdfUrl, InsufficientCreditsError } from "../api";
import type { GenerationResult } from "../types";

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
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit() {
    setBusy(true);
    setError(null);
    try {
      onRevised(await editThread(threadId, message));
      setMessage("");
    } catch (e) {
      setError(e instanceof InsufficientCreditsError
        ? "You're out of credits."
        : e instanceof Error ? e.message : "Something went wrong");
    } finally {
      setBusy(false);
    }
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
          disabled={message.trim().length === 0 || busy}
          className="rounded-lg bg-gray-100 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-200 disabled:cursor-not-allowed disabled:opacity-50"
        >
          {busy ? "Sending…" : "Send"}
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
  const [question, setQuestion] = useState("");
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [cvResult, setCvResult] = useState<GenerationResult | null>(null);
  const [letterResult, setLetterResult] = useState<GenerationResult | null>(null);
  const [answerResult, setAnswerResult] = useState<GenerationResult | null>(null);

  const postingInput = mode === "url" ? { postingUrl } : { postingText };
  const canSubmitPosting = mode === "url" ? postingUrl.trim().length > 0 : postingText.trim().length > 0;

  function handleError(e: unknown) {
    setError(e instanceof InsufficientCreditsError
      ? "You're out of credits."
      : e instanceof Error ? e.message : "Something went wrong");
  }

  async function handleGenerateCv() {
    setBusy("cv");
    setError(null);
    try {
      setCvResult(await generateCv(postingInput));
    } catch (e) {
      handleError(e);
    } finally {
      setBusy(null);
    }
  }

  async function handleGenerateLetter() {
    setBusy("letter");
    setError(null);
    try {
      setLetterResult(await generateLetter(postingInput));
    } catch (e) {
      handleError(e);
    } finally {
      setBusy(null);
    }
  }

  async function handleAskQuestion() {
    setBusy("answer");
    setError(null);
    try {
      setAnswerResult(await askQuestion({ question, postingUrl: mode === "url" ? postingUrl : undefined }));
    } catch (e) {
      handleError(e);
    } finally {
      setBusy(null);
    }
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
          <input
            value={postingUrl}
            onChange={e => setPostingUrl(e.target.value)}
            placeholder="https://…"
            className="w-full rounded-lg border border-gray-200 p-2 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-blue-300"
          />
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
            disabled={!canSubmitPosting || busy !== null}
            className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:bg-gray-300"
          >
            {busy === "cv" ? "Generating…" : "Generate CV"}
          </button>
          <button
            onClick={handleGenerateLetter}
            disabled={!canSubmitPosting || busy !== null}
            className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:bg-gray-300"
          >
            {busy === "letter" ? "Generating…" : "Generate cover letter"}
          </button>
        </div>
      </div>

      {cvResult && (
        <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
          <p className="mb-2 text-sm font-medium text-gray-700">CV ready</p>
          <a
            href={cvPdfUrl(cvResult.threadId)}
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
            disabled={question.trim().length === 0 || busy !== null}
            className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:bg-gray-300"
          >
            {busy === "answer" ? "Asking…" : "Ask"}
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
