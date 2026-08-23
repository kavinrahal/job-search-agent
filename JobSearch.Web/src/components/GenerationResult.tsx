import { useEffect, useState } from "react";
import { threadPdfUrl, threadDocxUrl } from "../api";
import { useEditThread } from "../hooks/useGeneration";
import type { GenerationResult } from "../types";
import { ResumePdfViewer } from "./ResumePdfViewer";
import { INPUT, SECONDARY_BUTTON } from "../lib/styles";

// The rendered result of a CV/cover-letter generation, extracted from GeneratePage so the
// Discover tab's one-tap drawer shows the identical thing (same PDF preview, revision box,
// downloads and accuracy warnings) rather than a second, drifting copy of it.

// Non-blocking by design (see AccuracyVerifierAgent's own comment) — the content above this
// is already generated and downloadable either way, this just tells the user what to
// double-check before they actually submit it somewhere.
export function AccuracyWarningBanner({ warnings }: { warnings?: string[] }) {
  if (!warnings || warnings.length === 0) return null;
  return (
    <div className="mb-3 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-300">
      <p className="font-medium">Worth double-checking before you send this:</p>
      <ul className="mt-1 list-inside list-disc space-y-0.5">
        {warnings.map((w, i) => <li key={i}>{w}</li>)}
      </ul>
    </div>
  );
}

// Same input drives two things depending on the thread's state (the backend endpoint
// handles both transparently): replying to an answer-agent follow-up question, or
// requesting a revision to an already-complete CV/letter/answer.
export function RevisionBox({ threadId, placeholder, onRevised }: {
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
          className={INPUT}
        />
        <button onClick={handleSubmit} disabled={message.trim().length === 0 || loading} className={SECONDARY_BUTTON}>
          {loading ? "Sending…" : "Send"}
        </button>
      </div>
      {error && <p className="mt-1 text-xs text-red-600 dark:text-red-400">{error}</p>}
    </div>
  );
}

export function CvResult({ result, onRevised }: {
  result: GenerationResult; onRevised: (r: GenerationResult) => void;
}) {
  // Revising a CV keeps the same threadId, so the PDF URL doesn't change on its own — this
  // cache-busts it after every revision so the preview reflects the new content instead of
  // whatever react-pdf (or the browser) cached under that same URL. Owned here rather than by
  // each caller so neither call site can forget it; it only has to change, never reset.
  const [revision, setRevision] = useState(0);

  function handleRevised(revised: GenerationResult) {
    setRevision(r => r + 1);
    onRevised(revised);
  }

  return (
    <>
      <AccuracyWarningBanner warnings={result.accuracyWarnings} />
      <ResumePdfViewer source={`${threadPdfUrl(result.threadId)}?r=${revision}`} />
      <a href={threadPdfUrl(result.threadId)} className={`mt-3 inline-block ${SECONDARY_BUTTON}`}>
        Download PDF
      </a>
      <RevisionBox
        threadId={result.threadId}
        placeholder="Request changes (e.g. mention Docker experience)"
        onRevised={handleRevised}
      />
    </>
  );
}

export function LetterResult({ result, onRevised }: {
  result: GenerationResult; onRevised: (r: GenerationResult) => void;
}) {
  const [copied, setCopied] = useState(false);

  // Every fresh or revised letter is copied automatically — the point of generating one is to
  // paste it somewhere else (an application form, an email), so save that extra click.
  // Clipboard access can fail quietly (permissions, non-secure context), so the rejection
  // branch clears the flag rather than leaving a stale "Copied" from a previous letter. The
  // `cancelled` guard keeps a slow copy for a superseded letter from setting state after a
  // newer one has already landed.
  useEffect(() => {
    let cancelled = false;
    navigator.clipboard.writeText(result.text ?? "").then(
      () => { if (!cancelled) setCopied(true); },
      () => { if (!cancelled) setCopied(false); },
    );
    return () => { cancelled = true; };
  }, [result]);

  return (
    <>
      {copied && (
        <p className="mb-2 text-xs text-emerald-600 dark:text-emerald-400">Copied to clipboard.</p>
      )}
      <AccuracyWarningBanner warnings={result.accuracyWarnings} />
      <pre className="whitespace-pre-wrap font-sans text-sm text-gray-700 dark:text-gray-300">{result.text}</pre>
      <div className="mt-3 flex gap-2">
        <a href={threadPdfUrl(result.threadId)} className={`inline-block ${SECONDARY_BUTTON}`}>
          Download PDF
        </a>
        <a href={threadDocxUrl(result.threadId)} className={`inline-block ${SECONDARY_BUTTON}`}>
          Download Word
        </a>
      </div>
      <RevisionBox threadId={result.threadId} placeholder="Request changes" onRevised={onRevised} />
    </>
  );
}
