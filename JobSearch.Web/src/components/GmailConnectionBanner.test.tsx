/** @vitest-environment jsdom */
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { GmailConnectionBanner } from "./GmailConnectionBanner";

afterEach(cleanup);

// GmailConnectionBanner renders a react-router <Link>, which throws outside a Router context —
// every render below needs a MemoryRouter wrapper even though navigation itself isn't exercised.
function renderBanner(broken?: boolean) {
  return render(
    <MemoryRouter>
      <GmailConnectionBanner broken={broken} />
    </MemoryRouter>,
  );
}

describe("GmailConnectionBanner", () => {
  it("renders nothing when broken is undefined", () => {
    const { container } = renderBanner(undefined);
    expect(container.firstChild).toBeNull();
  });

  it("renders nothing when broken is false", () => {
    const { container } = renderBanner(false);
    expect(container.firstChild).toBeNull();
  });

  it("renders the warning and a link to Sources when broken is true", () => {
    renderBanner(true);

    expect(screen.getByText("Gmail tracking has stopped.")).toBeTruthy();
    const link = screen.getByRole("link", { name: /Reconnect Gmail from Sources/ });
    expect(link.getAttribute("href")).toBe("/sources");
  });
});
