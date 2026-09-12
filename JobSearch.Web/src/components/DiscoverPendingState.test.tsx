/** @vitest-environment jsdom */
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { DiscoverPendingState } from "./DiscoverPendingState";

afterEach(cleanup);

// DiscoverPendingState renders a react-router Link (via Button href="/criteria"), which throws
// outside a Router context — same reasoning as DiscoverCriteriaGate.test.tsx.
function renderPending() {
  return render(
    <MemoryRouter>
      <DiscoverPendingState />
    </MemoryRouter>,
  );
}

describe("DiscoverPendingState", () => {
  it("explains matches are coming rather than reporting an error or dead end", () => {
    renderPending();

    expect(screen.getByText("Nothing delivered yet")).toBeTruthy();
    expect(screen.getByText(/next Discover run checks postings against your criteria/)).toBeTruthy();
  });

  it("does not reuse DiscoverCriteriaGate's incomplete-criteria copy", () => {
    renderPending();

    expect(screen.queryByText(/Finish your job criteria/)).toBeNull();
    expect(screen.queryByText(/Missing:/)).toBeNull();
  });

  it("links to the criteria page for a user who wants to double-check it", () => {
    renderPending();

    const link = screen.getByRole("link", { name: "Review criteria" });
    expect(link.getAttribute("href")).toBe("/criteria");
  });
});
