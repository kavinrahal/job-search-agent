// Remembers the thread id of the most recent generation per flow, so an accidental refresh can
// restore the result instead of dropping the user back on an empty form. Scoped to the two flows
// that actually show a result today (GeneratePage, GenerationDrawer) — not a general "resume any
// interrupted work anywhere" system.
//
// Entries older than 24h are treated as absent and cleared: restoring a day-old draft the user
// has long moved on from would be more surprising than helpful.

const MAX_AGE_MS = 24 * 60 * 60 * 1000;

interface StoredThread {
  threadId: number;
  timestamp: number;
}

export function rememberThread(key: string, threadId: number): void {
  try {
    localStorage.setItem(key, JSON.stringify({ threadId, timestamp: Date.now() } satisfies StoredThread));
  } catch {
    // Storage disabled (private mode / quota). Restore is a nicety, not load-bearing.
  }
}

export function recallThread(key: string): number | null {
  try {
    const raw = localStorage.getItem(key);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as Partial<StoredThread>;
    if (typeof parsed.threadId !== "number" || typeof parsed.timestamp !== "number") return null;
    if (Date.now() - parsed.timestamp > MAX_AGE_MS) {
      localStorage.removeItem(key);
      return null;
    }
    return parsed.threadId;
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
