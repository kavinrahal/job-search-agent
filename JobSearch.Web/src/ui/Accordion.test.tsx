/** @vitest-environment jsdom */
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { Accordion } from "./Accordion";

afterEach(cleanup);

const ITEMS = [
  { question: "Is this free right now?", answer: "Yes." },
  { question: "Do I have to connect Gmail?", answer: "No." },
];

describe("Accordion", () => {
  it("starts with every item collapsed", () => {
    render(<Accordion items={ITEMS} />);
    for (const button of screen.getAllByRole("button")) {
      expect(button.getAttribute("aria-expanded")).toBe("false");
    }
  });

  it("expands an item on click and marks its panel visible", () => {
    render(<Accordion items={ITEMS} />);
    const trigger = screen.getByText(ITEMS[0].question).closest("button")!;
    fireEvent.click(trigger);

    expect(trigger.getAttribute("aria-expanded")).toBe("true");
    const panelId = trigger.getAttribute("aria-controls")!;
    expect(document.getElementById(panelId)!.getAttribute("aria-hidden")).toBe("false");
  });

  it("closes the previously open item when a different one opens", () => {
    render(<Accordion items={ITEMS} />);
    const first = screen.getByText(ITEMS[0].question).closest("button")!;
    const second = screen.getByText(ITEMS[1].question).closest("button")!;

    fireEvent.click(first);
    expect(first.getAttribute("aria-expanded")).toBe("true");

    fireEvent.click(second);
    expect(first.getAttribute("aria-expanded")).toBe("false");
    expect(second.getAttribute("aria-expanded")).toBe("true");
  });

  it("collapses an open item when clicked again", () => {
    render(<Accordion items={ITEMS} />);
    const trigger = screen.getByText(ITEMS[0].question).closest("button")!;
    fireEvent.click(trigger);
    fireEvent.click(trigger);
    expect(trigger.getAttribute("aria-expanded")).toBe("false");
  });
});
