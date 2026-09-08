/** @vitest-environment jsdom */
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";

// GenerationResult.tsx also exports CvResult, which pulls in ResumePdfViewer -> react-pdf/pdfjs.
// pdfjs touches DOMMatrix at import time, which jsdom doesn't provide — stub it out so this file
// can test AccuracyWarningBanner in isolation without a real PDF.js/canvas environment.
vi.mock("./ResumePdfViewer", () => ({ ResumePdfViewer: () => null }));

const { AccuracyWarningBanner } = await import("./GenerationResult");

afterEach(cleanup);

describe("AccuracyWarningBanner", () => {
  it("renders nothing when warnings is undefined", () => {
    const { container } = render(<AccuracyWarningBanner />);
    expect(container.firstChild).toBeNull();
  });

  it("renders nothing when warnings is an empty array", () => {
    const { container } = render(<AccuracyWarningBanner warnings={[]} />);
    expect(container.firstChild).toBeNull();
  });

  it("renders one list item per warning, in order, when warnings are present", () => {
    const warnings = [
      "The phrase \"led a team of six\" does not appear anywhere in your background.",
      "\"Certified Scrum Master\" isn't mentioned in your background or CV.",
    ];
    render(<AccuracyWarningBanner warnings={warnings} />);

    expect(screen.getByText("Worth double-checking before you send this:")).toBeTruthy();
    expect(screen.getAllByRole("listitem").map(li => li.textContent)).toEqual(warnings);
  });
});
