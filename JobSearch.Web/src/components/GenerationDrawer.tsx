import { useEffect, useState } from "react";
import { useGenerateCv, useGenerateLetter } from "../hooks/useGeneration";
import type { GenerationResult } from "../types";
import { GeneratingIndicator } from "./GeneratingIndicator";
import { CvResult, LetterResult } from "./GenerationResult";
import { PRIMARY_BUTTON, SECONDARY_BUTTON } from "../lib/styles";

export type GenerationKind = "cv" | "letter";

// One-tap CV/cover-letter generation for a Discover card, without leaving the list. Renders
// the same CvResult/LetterResult the Generate page uses, so there's one implementation of the
// PDF preview, revision box and downloads rather than a second copy that drifts.
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

  const action = kind === "cv" ? generateCv : generateLetter;
  const label = kind === "cv" ? "CV" : "cover letter";

  // Escape closes, and the page behind is locked from scrolling while the drawer is open —
  // without this the list keeps scrolling under the overlay on touch devices.
  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") onClose();
    }
    document.addEventListener("keydown", onKeyDown);
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", onKeyDown);
      document.body.style.overflow = previousOverflow;
    };
  }, [onClose]);

  async function handleConfirm() {
    setConfirmed(true);
    setResult(await action.execute({ discoveryId }));
  }

  return (
    <div className="fixed inset-0 z-50 flex justify-end">
      <div
        className="absolute inset-0 bg-gray-900/40 backdrop-blur-sm"
        onClick={onClose}
        aria-hidden="true"
      />

      <div
        role="dialog"
        aria-modal="true"
        aria-label={`Generate ${label}`}
        className="relative flex h-full w-full max-w-lg flex-col overflow-y-auto border-l border-gray-200 bg-white shadow-xl dark:border-gray-800 dark:bg-gray-900"
      >
        <div className="sticky top-0 z-10 flex items-start justify-between gap-3 border-b border-gray-100 bg-white/95 p-4 backdrop-blur dark:border-gray-800 dark:bg-gray-900/95">
          <div className="min-w-0">
            <p className="truncate text-sm font-semibold text-gray-700 dark:text-gray-200">{title}</p>
            <p className="truncate text-xs text-gray-400 dark:text-gray-500">{company}</p>
          </div>
          <button
            onClick={onClose}
            aria-label="Close"
            className="shrink-0 rounded-lg p-1 text-gray-400 transition-colors hover:bg-gray-100 hover:text-gray-600 dark:hover:bg-gray-800 dark:hover:text-gray-300"
          >
            ✕
          </button>
        </div>

        <div className="p-4">
          {!confirmed && (
            <div>
              <p className="text-sm text-gray-600 dark:text-gray-300">
                Generate a tailored {label} for this role? This uses 1 credit.
              </p>
              <div className="mt-4 flex gap-2">
                <button onClick={handleConfirm} className={PRIMARY_BUTTON}>
                  Generate {label}
                </button>
                <button onClick={onClose} className={SECONDARY_BUTTON}>
                  Cancel
                </button>
              </div>
            </div>
          )}

          {action.loading && <GeneratingIndicator kind={kind} />}

          {action.error && (
            <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-950/30 dark:text-red-400">
              {action.error}
            </div>
          )}

          {result && <div className="animate-fade-in-up">{kind === "cv"
            ? <CvResult result={result} onRevised={setResult} />
            : <LetterResult result={result} onRevised={setResult} />}
          </div>}
        </div>
      </div>
    </div>
  );
}
