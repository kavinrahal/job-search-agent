/** @vitest-environment jsdom */
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";

// Smoke test only — no existing test infra for the Sources/help pages to extend (see
// GmailConnectionBanner.test.tsx for the closest precedent: mock the hook, assert on rendered
// text/links rather than exercising the real fetch or router navigation).
vi.mock("../hooks/useSources", () => ({
  useGmailForwardingStatus: () => ({
    data: { address: "alerts+abc123@parse.worksanta.com", status: "not_added", filterInstalled: false },
    loading: false,
    error: null,
    reload: vi.fn(),
  }),
}));

const { GmailForwardingHelpPage } = await import("./GmailForwardingHelpPage");

afterEach(cleanup);

describe("GmailForwardingHelpPage", () => {
  it("renders the walkthrough steps, the reused forwarding address, and a link back to Sources", () => {
    render(
      <MemoryRouter>
        <GmailForwardingHelpPage />
      </MemoryRouter>,
    );

    expect(screen.getByText("Set up Gmail forwarding, step by step")).toBeTruthy();
    expect(screen.getByText("alerts+abc123@parse.worksanta.com")).toBeTruthy();
    expect(screen.getByRole("link", { name: "← Back to Sources" }).getAttribute("href")).toBe("/sources");
    expect(screen.getByText("Open Gmail on desktop and go to Settings")).toBeTruthy();
    expect(screen.getByText("Come back here and check status")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Check status" })).toBeTruthy();
  });
});
