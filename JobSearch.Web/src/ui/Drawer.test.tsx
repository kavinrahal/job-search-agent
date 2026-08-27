/** @vitest-environment jsdom */
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { useState } from "react";
import { Drawer } from "./Drawer";

afterEach(cleanup);

// fireEvent, not a raw dispatchEvent: React state updates triggered by the handler have to be
// flushed inside act() or the assertions run against the previous render.
function press(key: string, shiftKey = false) {
  fireEvent.keyDown(document, { key, shiftKey });
}

function Harness({ onClose }: { onClose?: () => void }) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button type="button" onClick={() => setOpen(true)}>
        Open
      </button>
      <Drawer
        open={open}
        onClose={() => {
          setOpen(false);
          onClose?.();
        }}
        title="Victorian Government"
        description="Senior Developer"
        footer={
          <button type="button" onClick={() => setOpen(false)}>
            Generate CV
          </button>
        }
      >
        <button type="button">Body action</button>
      </Drawer>
    </>
  );
}

// jsdom reports offsetParent as null for everything, which the focus-trap's visibility filter would
// read as "nothing is focusable". Making it non-null is what lets these tests exercise the real
// trap rather than its empty-list escape hatch.
function makeElementsVisible() {
  Object.defineProperty(HTMLElement.prototype, "offsetParent", {
    configurable: true,
    get() {
      return this.parentNode;
    },
  });
}
makeElementsVisible();

describe("Drawer dismissal", () => {
  it("closes on Escape", () => {
    const onClose = vi.fn();
    render(<Harness onClose={onClose} />);
    fireEvent.click(screen.getByText("Open"));
    expect(screen.getByRole("dialog")).toBeTruthy();

    press("Escape");
    expect(onClose).toHaveBeenCalledOnce();
    expect(screen.queryByRole("dialog")).toBeNull();
  });

  it("closes on a click outside the panel", () => {
    const onClose = vi.fn();
    render(<Harness onClose={onClose} />);
    fireEvent.click(screen.getByText("Open"));

    // The scrim is the dialog's sibling inside the fixed overlay.
    const scrim = screen.getByRole("dialog").parentElement!.firstElementChild as HTMLElement;
    fireEvent.click(scrim);
    expect(onClose).toHaveBeenCalledOnce();
  });

  it("closes from its own close button", () => {
    const onClose = vi.fn();
    render(<Harness onClose={onClose} />);
    fireEvent.click(screen.getByText("Open"));
    fireEvent.click(screen.getByLabelText("Close"));
    expect(onClose).toHaveBeenCalledOnce();
  });
});

describe("Drawer focus management", () => {
  it("moves focus into the panel on open", () => {
    render(<Harness />);
    fireEvent.click(screen.getByText("Open"));
    expect(screen.getByRole("dialog").contains(document.activeElement)).toBe(true);
  });

  it("returns focus to the trigger on close", () => {
    render(<Harness />);
    const trigger = screen.getByText("Open");
    trigger.focus();
    fireEvent.click(trigger);
    expect(document.activeElement).not.toBe(trigger);

    press("Escape");
    expect(document.activeElement).toBe(trigger);
  });

  it("wraps Tab from the last control back to the first", () => {
    render(<Harness />);
    fireEvent.click(screen.getByText("Open"));

    const dialog = screen.getByRole("dialog");
    const focusable = Array.from(dialog.querySelectorAll<HTMLElement>("button"));
    const first = focusable.at(0)!;
    const last = focusable.at(-1)!;

    last.focus();
    press("Tab");
    expect(document.activeElement).toBe(first);
  });

  it("wraps Shift+Tab from the first control back to the last", () => {
    render(<Harness />);
    fireEvent.click(screen.getByText("Open"));

    const dialog = screen.getByRole("dialog");
    const focusable = Array.from(dialog.querySelectorAll<HTMLElement>("button"));
    const first = focusable.at(0)!;
    const last = focusable.at(-1)!;

    first.focus();
    press("Tab", true);
    expect(document.activeElement).toBe(last);
  });
});

describe("Drawer background scroll lock", () => {
  it("locks the body while open and restores the previous value on close", () => {
    document.body.style.overflow = "scroll";
    render(<Harness />);

    fireEvent.click(screen.getByText("Open"));
    expect(document.body.style.overflow).toBe("hidden");

    press("Escape");
    // Restored, not cleared: clearing would let two stacked overlays leave the page unscrollable.
    expect(document.body.style.overflow).toBe("scroll");
    document.body.style.overflow = "";
  });
});

describe("Drawer semantics", () => {
  it("is a modal dialog named by its title", () => {
    render(<Harness />);
    fireEvent.click(screen.getByText("Open"));
    const dialog = screen.getByRole("dialog", { name: "Victorian Government" });
    expect(dialog.getAttribute("aria-modal")).toBe("true");
  });

  it("renders nothing at all when closed", () => {
    render(<Harness />);
    expect(screen.queryByRole("dialog")).toBeNull();
    expect(screen.queryByText("Body action")).toBeNull();
  });
});
