/** @vitest-environment jsdom */
import { afterEach, describe, expect, it, vi } from "vitest";
import { rememberPending, resolveThread, recallThread, forgetThread } from "./lastGeneration";

const KEY = "lastCvThreadId";

afterEach(() => {
  localStorage.clear();
  vi.useRealTimers();
});

// Regression coverage for the credit-charged-on-refresh bug: rememberPending must be callable
// (and readable back) *before* the generation request resolves, not only after — that's what
// lets useGenerationRecovery find an in-flight (or already-finished-but-unreceived) generation
// after a page refresh instead of losing track of it entirely. See lastGeneration.ts's own
// top-of-file comment for the full story.
describe("rememberPending / recallThread", () => {
  it("is readable immediately, before any threadId is known", () => {
    rememberPending(KEY, "req-1");

    const stored = recallThread(KEY);

    expect(stored).toEqual({ requestId: "req-1", threadId: null, timestamp: expect.any(Number) });
  });

  it("returns null when nothing has been stored yet", () => {
    expect(recallThread(KEY)).toBeNull();
  });
});

describe("resolveThread", () => {
  it("upgrades a pending entry with the resolved threadId, keeping the same requestId", () => {
    rememberPending(KEY, "req-2");
    resolveThread(KEY, "req-2", 42);

    expect(recallThread(KEY)).toEqual({ requestId: "req-2", threadId: 42, timestamp: expect.any(Number) });
  });

  it("can also write a fresh entry directly, without a prior rememberPending call", () => {
    resolveThread(KEY, "req-3", 7);

    expect(recallThread(KEY)).toEqual({ requestId: "req-3", threadId: 7, timestamp: expect.any(Number) });
  });
});

describe("forgetThread", () => {
  it("clears a stored entry", () => {
    resolveThread(KEY, "req-4", 1);
    forgetThread(KEY);

    expect(recallThread(KEY)).toBeNull();
  });

  it("is a no-op when nothing was stored", () => {
    expect(() => forgetThread(KEY)).not.toThrow();
  });
});

describe("recallThread — 24h expiry", () => {
  it("returns an entry stored just now", () => {
    resolveThread(KEY, "req-5", 5);
    expect(recallThread(KEY)).not.toBeNull();
  });

  it("treats an entry older than 24h as absent and clears it", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-01-01T00:00:00Z"));
    resolveThread(KEY, "req-6", 6);

    vi.setSystemTime(new Date("2026-01-02T00:00:01Z")); // 24h + 1s later

    expect(recallThread(KEY)).toBeNull();
    // Clearing on read matters: a stale entry left behind would otherwise keep 404ing
    // fetchThreadByRequest on every future mount instead of falling through to a fresh attempt.
    expect(localStorage.getItem(KEY)).toBeNull();
  });

  it("still returns an entry just under the 24h boundary", () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-01-01T00:00:00Z"));
    resolveThread(KEY, "req-7", 7);

    vi.setSystemTime(new Date("2026-01-01T23:59:59Z"));

    expect(recallThread(KEY)).not.toBeNull();
  });
});

describe("recallThread — malformed storage", () => {
  it("returns null for invalid JSON instead of throwing", () => {
    localStorage.setItem(KEY, "{not json");
    expect(() => recallThread(KEY)).not.toThrow();
    expect(recallThread(KEY)).toBeNull();
  });

  it("returns null when requestId is missing or the wrong type", () => {
    localStorage.setItem(KEY, JSON.stringify({ threadId: 1, timestamp: Date.now() }));
    expect(recallThread(KEY)).toBeNull();
  });

  it("treats a missing threadId as still-pending (null), not malformed", () => {
    localStorage.setItem(KEY, JSON.stringify({ requestId: "req-8", timestamp: Date.now() }));
    expect(recallThread(KEY)).toEqual({ requestId: "req-8", threadId: null, timestamp: expect.any(Number) });
  });
});
