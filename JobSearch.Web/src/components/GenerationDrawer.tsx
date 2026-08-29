import { useEffect, useState } from "react";
import { useGenerateCv, useGenerateLetter } from "../hooks/useGeneration";
import { useMeContext } from "../hooks/useMeContext";
import { fetchThread } from "../api";
import { rememberThread, recallThread, forgetThread } from "../lib/lastGeneration";
import type { GenerationResult } from "../types";
import { GeneratingIndicator } from "./GeneratingIndicator";
import { CvResult, LetterResult } from "./GenerationResult";
import { Drawer, Button, Callout } from "../ui";

export type GenerationKind = "cv" | "letter";

// One-tap CV/cover-letter generation for a Discover card, without leaving the list. Renders
// the same CvResult/LetterResult the Generate page uses, so there's one implementation of the
// PDF preview, revision box and downloads rather than a second copy that drifts.
//
// The panel itself is ui/Drawer, which already owns everything this used to hand-roll — escape
// to close, focus trap, background scroll lock, and (per Drawer's own note) a portal to escape
// AppShell's stacking context, which is exactly the bug the #63 gallery review caught here first.
export function GenerationDrawer({ discoveryId, kind, title, company, onClose }: {
  discoveryId: number;
  kind: GenerationKind;
  title: string;
  company: string;
  onClose: () => void;
}) {
  const [confirmed, setConfirmed] = useState(false);
  const [result, setResult] = useState<GenerationResult | null>(null);
  const generateCv = useGenerateCv();
  const generateLetter = useGenerateLetter();
  const { reloadMe } = useMeContext();

  const action = kind === "cv" ? generateCv : generateLetter;
  const label = kind === "cv" ? "CV" : "cover letter";
  // Per posting + kind, so each card restores its own draft independently.
  const storageKey = `lastGenThread:${discoveryId}:${kind}`;

  // If this posting+kind was generated within the last 24h, restore that result on open and skip
  // the "uses 1 credit" confirm step — the user already spent the credit. RevisionBox (inside the
  // result) is how they change it; there's no separate regenerate control. A dropped/expired
  // thread just clears its key and falls back to the normal confirm flow. Open == mount here: the
  // drawer is only rendered once a card's Generate is clicked.
  useEffect(() => {
    const threadId = recallThread(storageKey);
    if (threadId === null) return;
    fetchThread(threadId)
      .then(r => { setResult(r); setConfirmed(true); })
      .catch(() => forgetThread(storageKey));
    // eslint-disable-next-line react-hooks/exhaustive-deps -- restore runs once on open, storageKey is fixed for this drawer
  }, []);

  async function handleConfirm() {
    setConfirmed(true);
    const r = await action.execute({ discoveryId });
    setResult(r);
    rememberThread(storageKey, r.threadId);
    reloadMe();
  }

  return (
    <Drawer
      open
      onClose={onClose}
      title={title}
      description={company}
      footer={!confirmed && (
        <div className="flex gap-2">
          <Button onClick={handleConfirm}>Generate {label}</Button>
          <Button variant="subtle" onClick={onClose}>Cancel</Button>
        </div>
      )}
    >
      {!confirmed && (
        <p className="m-0 text-body text-muted">
          Generate a tailored {label} for this role? This uses 1 credit.
        </p>
      )}

      {action.loading && <GeneratingIndicator kind={kind} />}

      {action.error && <Callout variant="danger" title={action.error} />}

      {result && <div className="animate-fade-in-up">{kind === "cv"
        ? <CvResult result={result} onRevised={setResult} />
        : <LetterResult result={result} onRevised={setResult} />}
      </div>}
    </Drawer>
  );
}
