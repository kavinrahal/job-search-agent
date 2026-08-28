import { useEffect, useState } from "react";
import { threadPdfUrl, threadDocxUrl } from "../api";
import { useEditThread } from "../hooks/useGeneration";
import type { GenerationResult } from "../types";
import { ResumePdfViewer } from "./ResumePdfViewer";
import { Button, Input, WarningIcon } from "../ui";

// The rendered result of a CV/cover-letter generation, extracted from GeneratePage so the
// Discover tab's one-tap drawer shows the identical thing (same PDF preview, revision box,
// downloads and accuracy warnings) rather than a second, drifting copy of it.

// Non-blocking by design (see AccuracyVerifierAgent's own comment) — the content above this
// is already generated and downloadable either way, this just tells the user what to
// double-check before they actually submit it somewhere.
//
// Hand-rolled rather than Callout, despite Callout's own doc naming this exact banner as its
// main job in this product — Callout's title is a single bold lead-in with the rest flowing
// inline after it, and a warning list of unknown length needs a real <ul>, which does not fit
// inside that inline slot. Same bg-brass-wash/WarningIcon/text tokens Callout's warning variant
// uses, so it still reads as the same notice.
export function AccuracyWarningBanner({ warnings }: { warnings?: string[] }) {
  if (!warnings || warnings.length === 0) return null;
  return (
    <div className="mb-3 flex items-start gap-2.5 rounded-ctl bg-brass-wash px-3 py-2.5 text-control text-ink-2">
      <WarningIcon className="mt-0.5 h-3.5 w-3.5 flex-none text-brass" />
      <div className="min-w-0">
        <p className="m-0 font-[650] text-ink">Worth double-checking before you send this:</p>
        <ul className="m-0 mt-1 list-inside list-disc space-y-0.5 p-0">
          {warnings.map((w, i) => <li key={i}>{w}</li>)}
        </ul>
      </div>
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
      <div className="flex items-end gap-2">
        <Input
          label="Revision request"
          className="flex-1"
          value={message}
          onChange={e => setMessage(e.target.value)}
          placeholder={placeholder}
        />
        <Button variant="ghost" size="sm" onClick={handleSubmit} disabled={message.trim().length === 0 || loading} loading={loading}>
          {loading ? "Sending…" : "Send"}
        </Button>
      </div>
      {error && <p className="mt-1 text-caption text-ember">{error}</p>}
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
      <Button href={threadPdfUrl(result.threadId)} variant="ghost" size="sm" className="mt-3">
        Download PDF
      </Button>
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
        <p className="mb-2 text-caption text-pos">Copied to clipboard.</p>
      )}
      <AccuracyWarningBanner warnings={result.accuracyWarnings} />
      <pre className="whitespace-pre-wrap font-sans text-body text-ink-2">{result.text}</pre>
      <div className="mt-3 flex gap-2">
        <Button href={threadPdfUrl(result.threadId)} variant="ghost" size="sm">
          Download PDF
        </Button>
        <Button href={threadDocxUrl(result.threadId)} variant="ghost" size="sm">
          Download Word
        </Button>
      </div>
      <RevisionBox threadId={result.threadId} placeholder="Request changes" onRevised={onRevised} />
    </>
  );
}
