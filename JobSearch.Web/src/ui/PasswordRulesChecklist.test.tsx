/** @vitest-environment jsdom */
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { PasswordRulesChecklist, type PasswordRuleState } from "./PasswordRulesChecklist";

afterEach(cleanup);

const RULES: PasswordRuleState[] = [
  { id: "len", label: "8 characters", met: true },
  { id: "lower", label: "Lowercase", met: true },
  { id: "upper", label: "Uppercase", met: false },
  { id: "num", label: "A number", met: false },
];

function items() {
  return screen.getAllByRole("listitem");
}

describe("PasswordRulesChecklist", () => {
  it("renders one item per rule, in the order given", () => {
    render(<PasswordRulesChecklist rules={RULES} />);
    expect(items().map(li => li.textContent)).toEqual(["8 characters", "Lowercase", "Uppercase", "A number"]);
  });

  it("distinguishes met from unmet by more than colour", () => {
    render(<PasswordRulesChecklist rules={RULES} />);
    const [met, , unmet] = items();

    // A tick for met, an empty circle for unmet — the shape carries the state, so the list is
    // still readable to someone who cannot tell pos green from faint grey.
    expect(met.querySelector("svg path")).toBeTruthy();
    expect(unmet.querySelector("svg circle")).toBeTruthy();
    expect(met.querySelector("svg circle")).toBeNull();
  });

  it("summarises what is left in a single polite live region", () => {
    render(<PasswordRulesChecklist rules={RULES} />);
    const status = screen.getByText("2 password requirements left.");
    expect(status.getAttribute("aria-live")).toBe("polite");
    // One region, not one per rule: four announcements for one keystroke is unusable.
    expect(document.querySelectorAll("[aria-live]")).toHaveLength(1);
  });

  it("uses the singular when one rule is left", () => {
    render(<PasswordRulesChecklist rules={RULES.map((r, i) => ({ ...r, met: i < 3 }))} />);
    expect(screen.getByText("1 password requirement left.")).toBeTruthy();
  });

  it("says so once every rule is satisfied", () => {
    render(<PasswordRulesChecklist rules={RULES.map(r => ({ ...r, met: true }))} />);
    expect(screen.getByText("All password requirements met.")).toBeTruthy();
  });

  it("renders an empty list without crashing, since the rules module owns the list", () => {
    render(<PasswordRulesChecklist rules={[]} />);
    expect(screen.queryAllByRole("listitem")).toHaveLength(0);
    expect(screen.getByText("All password requirements met.")).toBeTruthy();
  });
});
