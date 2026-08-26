import { useEffect, useRef, useState } from "react";
import { fetchResumePreview, type ResumeDraft } from "../api";

const DEBOUNCE_MS = 400;

// Re-fetches the resume builder's live preview markdown ~400ms after `draft` stops changing —
// fast enough to feel live, slow enough not to hit POST /resume/preview on every keystroke.
// Not built on useAsyncData: that hook fires immediately on every dependency change (the
// GET/list-page pattern), which is exactly wrong here. This also has to ignore a response that
// resolves after a newer draft has already superseded it (a real risk once debounced requests
// can be in flight while the user keeps typing), which useAsyncData's callers never needed
// since a GET there doesn't race a fast-changing local edit.
// `enabled: false` (e.g. while the page's own resume/profile/background data is still loading)
// skips fetching entirely rather than firing a preview request for a draft that's still the
// placeholder EMPTY value — that request would be harmless (just briefly wrong output), but
// there's no reason to make it.
export function useDebouncedPreview(draft: ResumeDraft, enabled = true) {
  const [markdown, setMarkdown] = useState<string | null>(null);
  const [loading, setLoading] = useState(enabled);
  const [error, setError] = useState<string | null>(null);
  const requestId = useRef(0);

  // draft is expected to be a fresh object per render (same "you bring your own deps" contract
  // as useAsyncData/useSyncedState) — stringifying it is the actual dependency, so the debounce
  // timer only restarts when the draft's *content* changes, not on every parent re-render.
  const draftKey = JSON.stringify(draft);

  useEffect(() => {
    if (!enabled) return;
    // Signals "a fresh preview is now pending" the moment the draft changes, not just once the
    // debounced fetch actually starts — same legitimate "sync local state to an external async
    // operation" case useSyncedState's own effect documents, not derived state computable during
    // render (there's nothing to derive it from; the draft change itself is the event).
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setLoading(true);
    const timer = setTimeout(() => {
      const id = ++requestId.current;
      fetchResumePreview(draft)
        .then(({ markdown }) => {
          if (id !== requestId.current) return; // superseded by a newer draft, ignore
          setMarkdown(markdown);
          setLoading(false);
          setError(null);
        })
        .catch((e: unknown) => {
          if (id !== requestId.current) return;
          setError(e instanceof Error ? e.message : "Couldn't load the preview");
          setLoading(false);
        });
    }, DEBOUNCE_MS);

    return () => clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [draftKey, enabled]);

  return { markdown, loading, error };
}
