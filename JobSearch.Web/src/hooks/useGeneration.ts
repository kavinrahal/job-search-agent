import { useEffect, useRef, useState } from "react";
import { generateCv, generateLetter, askQuestion, editThread, searchPostingCandidates, fetchThread, fetchThreadByRequest } from "../api";
import { useAsyncAction } from "./useAsync";
import { recallThread, resolveThread, forgetThread } from "../lib/lastGeneration";
import type { GenerationResult } from "../types";

export function useGenerateCv() {
  return useAsyncAction(generateCv);
}

export function useGenerateLetter() {
  return useAsyncAction(generateLetter);
}

export function useAskQuestion() {
  return useAsyncAction(askQuestion);
}

export function useEditThread() {
  return useAsyncAction(editThread);
}

export function useSearchPostingCandidates() {
  return useAsyncAction(searchPostingCandidates);
}

const POLL_INTERVAL_MS = 3_000;
// Generous relative to a normal Claude generation call — long enough that a slow-but-genuine
// in-flight generation is still found, short enough that a truly abandoned/failed one (already
// refunded by WithCreditAsync's catch, see Program.cs) doesn't leave the page stuck "restoring"
// forever.
const POLL_TIMEOUT_MS = 90_000;

// Restores a CV/cover letter after an accidental refresh — including a refresh that happened
// *before* the original POST /cv or /letter response ever arrived, the exact "credit taken,
// nothing to show" bug: the backend has no cancellation wired to these requests (see Program.cs's
// WithCreditAsync), so the generation keeps running and completing server-side regardless of the
// client reloading. A stored entry with a resolved threadId (the request finished before or
// shortly after the refresh) is fetched directly, same as before. A stored entry with only a
// pending requestId means the response never made it back — poll by-request until it shows up
// or POLL_TIMEOUT_MS passes, at which point it's given up on and the key is cleared so a fresh
// attempt can be made. Mount-only, one instance per flow (GeneratePage/GenerationDrawer each call
// this once per artifact type it tracks).
export function useGenerationRecovery(key: string, onRestored: (result: GenerationResult) => void): boolean {
  // Lazy initializers, not state set from inside the effect below — recallThread only ever
  // needs to run once, at mount, and computing "are we recovering" up front (rather than
  // synchronously inside the effect) keeps this out of react-hooks' set-state-in-effect rule.
  const [stored] = useState(() => recallThread(key));
  const [recovering, setRecovering] = useState(() => stored !== null && stored.threadId === null);
  const onRestoredRef = useRef(onRestored);
  useEffect(() => { onRestoredRef.current = onRestored; }, [onRestored]);

  useEffect(() => {
    if (!stored) return;

    if (stored.threadId !== null) {
      fetchThread(stored.threadId).then(r => onRestoredRef.current(r)).catch(() => forgetThread(key));
      return;
    }

    let cancelled = false;
    let timer: ReturnType<typeof setTimeout> | undefined;
    const deadline = Date.now() + POLL_TIMEOUT_MS;

    const poll = () => {
      fetchThreadByRequest(stored.requestId)
        .then(result => {
          if (cancelled) return;
          resolveThread(key, stored.requestId, result.threadId);
          setRecovering(false);
          onRestoredRef.current(result);
        })
        .catch(() => {
          if (cancelled) return;
          if (Date.now() >= deadline) {
            forgetThread(key);
            setRecovering(false);
            return;
          }
          timer = setTimeout(poll, POLL_INTERVAL_MS);
        });
    };
    poll();

    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- restore runs once on mount; key is fixed per caller
  }, []);

  return recovering;
}
