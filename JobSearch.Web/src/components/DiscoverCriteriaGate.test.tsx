/** @vitest-environment jsdom */
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { DiscoverCriteriaGate } from "./DiscoverCriteriaGate";

afterEach(cleanup);

// DiscoverCriteriaGate renders a react-router Link (via Button href="/criteria"), which throws
// outside a Router context — every render below needs a MemoryRouter wrapper even though
// navigation itself isn't exercised. Same pattern as GmailConnectionBanner.test.tsx.
function renderGate(missing: { key: string; label: string }[]) {
  return render(
    <MemoryRouter>
      <DiscoverCriteriaGate missing={missing} />
    </MemoryRouter>,
  );
}

describe("DiscoverCriteriaGate", () => {
  it("explains why Discover is blocked and lists the missing fields", () => {
    renderGate([{ key: "location", label: "Location" }, { key: "salary", label: "Salary" }]);

    expect(screen.getByText("Finish your job criteria to start discovering matches")).toBeTruthy();
    expect(screen.getByText(/Missing: Location, Salary\./)).toBeTruthy();
  });

  it("links to the criteria page as the one-click fix", () => {
    renderGate([{ key: "location", label: "Location" }]);

    const link = screen.getByRole("link", { name: "Finish criteria" });
    expect(link.getAttribute("href")).toBe("/criteria");
  });
});
