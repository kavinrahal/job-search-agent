/** @vitest-environment jsdom */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { CountUp } from "./CountUp";

// jsdom implements neither IntersectionObserver nor a configurable matchMedia, and CountUp branches
// on both. Stubbing them is what makes these tests exercise the real paths rather than the
// "neither exists, so render the final value" fallback, which would pass vacuously.

let observed: Element[] = [];
let triggerIntersection: () => void = () => {};

function stubIntersectionObserver() {
  observed = [];
  class FakeObserver {
    callback: IntersectionObserverCallback;
    constructor(callback: IntersectionObserverCallback) {
      this.callback = callback;
      triggerIntersection = () =>
        this.callback([{ isIntersecting: true } as IntersectionObserverEntry], this as unknown as IntersectionObserver);
    }
    observe(el: Element) {
      observed.push(el);
    }
    disconnect() {}
    unobserve() {}
    takeRecords() {
      return [];
    }
    root = null;
    rootMargin = "";
    thresholds = [];
  }
  vi.stubGlobal("IntersectionObserver", FakeObserver);
}

function stubReducedMotion(reduce: boolean) {
  vi.stubGlobal("matchMedia", (query: string) => ({
    matches: reduce && query.includes("prefers-reduced-motion"),
    media: query,
    addEventListener: () => {},
    removeEventListener: () => {},
  }));
}

beforeEach(() => {
  stubIntersectionObserver();
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("CountUp under prefers-reduced-motion", () => {
  it("renders the final value immediately and never observes anything", () => {
    stubReducedMotion(true);
    render(<CountUp value={34} />);

    // No intermediate zero: a reader who will not see it move must never see a wrong number.
    expect(screen.getAllByText("34").length).toBeGreaterThan(0);
    expect(screen.queryByText("0")).toBeNull();
    // No observer means no animation was ever scheduled, rather than one that finished quickly.
    expect(observed).toHaveLength(0);
  });

  it("still animates when motion is welcome", () => {
    stubReducedMotion(false);
    render(<CountUp value={34} />);

    // Starts at zero and waits to be seen.
    expect(screen.getByText("0")).toBeTruthy();
    expect(observed).toHaveLength(1);
  });
});

describe("CountUp accessibility", () => {
  it("exposes the real value to assistive tech even mid-animation", () => {
    stubReducedMotion(false);
    render(<CountUp value={34} />);

    // The visible number is 0 at this point; the announced one is already correct, so a screen
    // reader cannot catch it part way through and report a number that was never true.
    const announced = screen.getByText("34");
    expect(announced.className).toContain("sr-only");
    expect(screen.getByText("0").getAttribute("aria-hidden")).toBe("true");
  });
});

describe("CountUp animation", () => {
  it("runs once and lands on the exact value", async () => {
    stubReducedMotion(false);
    render(<CountUp value={34} />);

    triggerIntersection();
    // Let the rAF loop finish. The easing is asymptotic, so the assertion is that it *lands*
    // exactly rather than stopping a pixel short at 33.
    await vi.waitFor(() => expect(screen.getAllByText("34").length).toBeGreaterThan(1), { timeout: 3000 });
  });
});
