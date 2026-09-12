import { useState } from "react";
import { useGenerateCv, useGenerateLetter, useGenerationRecovery } from "../hooks/useGeneration";
import { useMeContext } from "../hooks/useMeContext";
import { rememberPending, resolveThread, forgetThread } from "../lib/lastGeneration";
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
  // result) is how they change it; there's no separate regenerate control. A dropped/expired/
  // never-completed thread just clears its key and falls back to the normal confirm flow.
  // Also covers a refresh that closed the drawer before the original response arrived — see
  // useGenerationRecovery's own comment — by polling until the still-running generation finishes
  // rather than giving up on the first miss.
  const recovering = useGenerationRecovery(storageKey, r => { setResult(r); setConfirmed(true); });

  // rememberPending happens before execute(), not after — see GeneratePage's handleGenerateCv
  // for why (the same fix, same reasoning, for this flow's confirm-then-generate step instead).
  async function handleConfirm() {
    setConfirmed(true);
    const clientRequestId = crypto.randomUUID();
    rememberPending(storageKey, clientRequestId);
    try {
      const r = await action.execute({ discoveryId, clientRequestId });
      setResult(r);
      resolveThread(storageKey, clientRequestId, r.threadId);
      reloadMe();
    } catch (e) {
      forgetThread(storageKey);
      throw e;
    }
  }

  return (
    <Drawer
      open
      onClose={onClose}
      title={title}
      description={company}
      footer={!confirmed && !recovering && (
        <div className="flex gap-2">
          <Button onClick={handleConfirm}>Generate {label}</Button>
          <Button variant="subtle" onClick={onClose}>Cancel</Button>
        </div>
      )}
    >
      {!confirmed && !recovering && (
        <p className="m-0 text-body text-muted">
          Generate a tailored {label} for this role? This uses 1 credit.
        </p>
      )}

      {(action.loading || recovering) && <GeneratingIndicator kind={kind} />}

      {action.error && <Callout variant="danger" title={action.error} />}

      {result && <div className="animate-fade-in-up">{kind === "cv"
        ? <CvResult result={result} onRevised={setResult} />
        : <LetterResult result={result} onRevised={setResult} />}
      </div>}
    </Drawer>
  );
}
