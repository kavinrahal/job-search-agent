/**
 * @vitest-environment jsdom
 *
 * Per-file rather than a global vitest environment, so the 96 existing pure-logic tests keep
 * running in node and pay nothing for a DOM they never touch.
 */
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { useState } from "react";
import { SegmentedControl } from "./SegmentedControl";

afterEach(cleanup);

const SEGMENTS = [
  { value: "all", label: "All", count: 14 },
  { value: "strong", label: "Strong", count: 2 },
  { value: "good", label: "Good", count: 5 },
] as const;

type Value = (typeof SEGMENTS)[number]["value"];

function Harness({ onChange }: { onChange?: (v: Value) => void }) {
  const [value, setValue] = useState<Value>("all");
  return (
    <SegmentedControl
      label="Filter"
      value={value}
      segments={[...SEGMENTS]}
      onChange={v => {
        setValue(v);
        onChange?.(v);
      }}
    />
  );
}

function radios() {
  return screen.getAllByRole("radio");
}

describe("SegmentedControl keyboard navigation", () => {
  it("exposes exactly one tab stop, on the selected segment", () => {
    render(<Harness />);
    const [all, strong, good] = radios();
    expect(all.tabIndex).toBe(0);
    expect(strong.tabIndex).toBe(-1);
    expect(good.tabIndex).toBe(-1);
  });

  it("moves and selects with ArrowRight, and moves focus with it", () => {
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);
    radios()[0].focus();
    fireEvent.keyDown(radios()[0], { key: "ArrowRight" });

    expect(onChange).toHaveBeenCalledWith("strong");
    expect(radios()[1].getAttribute("aria-checked")).toBe("true");
    expect(document.activeElement).toBe(radios()[1]);
  });

  it("moves backwards with ArrowLeft", () => {
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);
    fireEvent.keyDown(radios()[0], { key: "ArrowRight" });
    fireEvent.keyDown(radios()[1], { key: "ArrowLeft" });

    expect(onChange).toHaveBeenLastCalledWith("all");
    expect(document.activeElement).toBe(radios()[0]);
  });

  it("wraps from the last segment to the first and back", () => {
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);
    // Backwards off the start lands on the last segment.
    fireEvent.keyDown(radios()[0], { key: "ArrowLeft" });
    expect(onChange).toHaveBeenLastCalledWith("good");

    // Forwards off the end comes back round to the first.
    fireEvent.keyDown(radios()[2], { key: "ArrowRight" });
    expect(onChange).toHaveBeenLastCalledWith("all");
  });

  it("jumps to the ends with Home and End", () => {
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);
    fireEvent.keyDown(radios()[0], { key: "End" });
    expect(onChange).toHaveBeenLastCalledWith("good");

    fireEvent.keyDown(radios()[2], { key: "Home" });
    expect(onChange).toHaveBeenLastCalledWith("all");
  });

  it("treats ArrowDown and ArrowUp as the vertical equivalents", () => {
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);
    fireEvent.keyDown(radios()[0], { key: "ArrowDown" });
    expect(onChange).toHaveBeenLastCalledWith("strong");
    fireEvent.keyDown(radios()[1], { key: "ArrowUp" });
    expect(onChange).toHaveBeenLastCalledWith("all");
  });

  it("ignores keys it does not own, so typing does not hijack the page", () => {
    const onChange = vi.fn();
    render(<Harness onChange={onChange} />);
    fireEvent.keyDown(radios()[0], { key: "a" });
    expect(onChange).not.toHaveBeenCalled();
  });

  it("names the group so it does not announce as an unlabelled radiogroup", () => {
    render(<Harness />);
    expect(screen.getByRole("radiogroup", { name: "Filter" })).toBeTruthy();
  });
});
