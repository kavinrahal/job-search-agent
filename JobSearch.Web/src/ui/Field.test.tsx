/** @vitest-environment jsdom */
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { Field, Input, Select, Textarea } from "./Field";

afterEach(cleanup);

// The point of Field is that a caller cannot get this wiring wrong, so these assert the wiring
// rather than the markup: every one of them would have failed against the CardEditor `Field` this
// replaces, which rendered a <label> with no htmlFor at all.

describe("Field accessibility wiring", () => {
  it("associates the label with the control, so clicking the label focuses it", () => {
    render(<Input label="Email" />);
    // getByLabelText resolves through htmlFor/id. If that link is missing, this throws.
    const input = screen.getByLabelText("Email");
    expect(input.tagName).toBe("INPUT");
  });

  it("points aria-describedby at the hint when there is no error", () => {
    render(<Input label="Job posting link" hint="Each generation uses 1 credit." />);
    const input = screen.getByLabelText("Job posting link");
    const describedBy = input.getAttribute("aria-describedby");

    expect(describedBy).toBeTruthy();
    expect(document.getElementById(describedBy!)?.textContent).toBe("Each generation uses 1 credit.");
    expect(input.getAttribute("aria-invalid")).toBeNull();
  });

  it("marks the control invalid and describes it by the error, not the hint", () => {
    render(<Input label="Email" hint="We never share this." error="Add everything after the @ to continue." />);
    const input = screen.getByLabelText("Email");
    const describedBy = input.getAttribute("aria-describedby");

    expect(input.getAttribute("aria-invalid")).toBe("true");
    expect(document.getElementById(describedBy!)?.textContent).toBe("Add everything after the @ to continue.");
    // The hint is gone rather than stacked, so assistive tech is not reading guidance the user has
    // already failed to follow alongside the failure itself.
    expect(screen.queryByText("We never share this.")).toBeNull();
  });

  it("announces the error politely once it appears", () => {
    render(<Input label="Email" error="Add everything after the @ to continue." />);
    expect(screen.getByText("Add everything after the @ to continue.").getAttribute("aria-live")).toBe("polite");
  });

  it("sets required on the control, not only the asterisk", () => {
    render(<Input label="Password" required />);
    expect(screen.getByLabelText(/Password/).hasAttribute("required")).toBe(true);
  });

  it("omits aria-describedby entirely when there is nothing to describe", () => {
    render(<Input label="Email" />);
    expect(screen.getByLabelText("Email").getAttribute("aria-describedby")).toBeNull();
  });

  it("gives each instance its own ids, so two fields on a page do not collide", () => {
    render(
      <>
        <Input label="Email" hint="one" />
        <Input label="Password" hint="two" />
      </>,
    );
    const a = screen.getByLabelText("Email");
    const b = screen.getByLabelText("Password");

    expect(a.id).not.toBe(b.id);
    expect(a.getAttribute("aria-describedby")).not.toBe(b.getAttribute("aria-describedby"));
  });

  it("wires Textarea and Select the same way", () => {
    render(
      <>
        <Textarea label="Summary" error="Too long." />
        <Select label="Country">
          <option>Australia</option>
        </Select>
      </>,
    );
    expect(screen.getByLabelText("Summary").getAttribute("aria-invalid")).toBe("true");
    expect(screen.getByLabelText("Country").tagName).toBe("SELECT");
  });

  it("hands the same ids to a custom control through the render prop", () => {
    render(
      <Field label="Custom" hint="A hint">
        {props => <input {...props} />}
      </Field>,
    );
    const input = screen.getByLabelText("Custom");
    expect(input.id).toBeTruthy();
    expect(input.getAttribute("aria-describedby")).toBeTruthy();
  });
});

describe("Input passthrough", () => {
  it("forwards the typing attributes that decide which mobile keyboard appears", () => {
    render(<Input label="Job posting link" type="url" inputMode="url" autoComplete="off" spellCheck={false} />);
    const input = screen.getByLabelText("Job posting link");

    expect(input.getAttribute("type")).toBe("url");
    expect(input.getAttribute("inputmode")).toBe("url");
    expect(input.getAttribute("autocomplete")).toBe("off");
    expect(input.getAttribute("spellcheck")).toBe("false");
  });
});
