// Remembers the in-progress/most-recent generation per flow, so an accidental refresh can
// restore the result instead of dropping the user back on an empty form — and, critically, can
// still find it even if the refresh happened *before* the original request's response ever came
// back. That case used to be a dead end: rememberThread only used to get called after the POST
// resolved, so a refresh mid-flight left nothing in storage at all, even though the backend has
// no cancellation wired to these requests (see Program.cs's WithCreditAsync) and keeps generating
// — and charging the credit for — a result the client can no longer reach. Storing the
// client-generated requestId up front, before the request is even sent, closes that gap: see
// useGenerationRecovery, which polls GET /threads/by-request/{requestId} for exactly this case.
//
// Scoped to the two flows that actually show a result today (GeneratePage, GenerationDrawer) —
// not a general "resume any interrupted work anywhere" system.
//
// Entries older than 24h are treated as absent and cleared: restoring a day-old draft the user
// has long moved on from would be more surprising than helpful.

const MAX_AGE_MS = 24 * 60 * 60 * 1000;

interface StoredThread {
  requestId: string;
  threadId: number | null;
  timestamp: number;
}

// Call before firing the generation request, not after — the whole point is to have a recovery
// handle in storage during the window where the response hasn't arrived yet.
export function rememberPending(key: string, requestId: string): void {
  try {
    localStorage.setItem(key, JSON.stringify({ requestId, threadId: null, timestamp: Date.now() } satisfies StoredThread));
  } catch {
    // Storage disabled (private mode / quota). Restore is a nicety, not load-bearing.
  }
}

// Call once the request resolves, upgrading the pending entry with the now-known threadId so a
// later restore can fetch it directly instead of polling by-request again.
export function resolveThread(key: string, requestId: string, threadId: number): void {
  try {
    localStorage.setItem(key, JSON.stringify({ requestId, threadId, timestamp: Date.now() } satisfies StoredThread));
  } catch {
    // See rememberPending.
  }
}

export function recallThread(key: string): StoredThread | null {
  try {
    const raw = localStorage.getItem(key);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<StoredThread>;
    if (typeof parsed.requestId !== "string" || typeof parsed.timestamp !== "number") return null;
    if (Date.now() - parsed.timestamp > MAX_AGE_MS) {
      localStorage.removeItem(key);
      return null;
    }
    return { requestId: parsed.requestId, threadId: typeof parsed.threadId === "number" ? parsed.threadId : null, timestamp: parsed.timestamp };
  } catch {
    return null;
  }
}

export function forgetThread(key: string): void {
  try {
    localStorage.removeItem(key);
  } catch {
    // Nothing to recover from — a stale key that can't be cleared is harmless, it just ages out.
  }
}
