/**
 * @vitest-environment jsdom
 */
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { useState } from "react";
import { JobCriteriaEditor } from "./JobCriteriaEditor";
import { parseJobCriteriaYaml, type JobCriteriaData } from "../lib/jobCriteriaYaml";

afterEach(cleanup);

function Harness({ initial = parseJobCriteriaYaml("") }: { initial?: JobCriteriaData }) {
  const [value, setValue] = useState<JobCriteriaData>(initial);
  return <JobCriteriaEditor value={value} onChange={setValue} tier="Tier1" />;
}

// The Skills topic card is collapsed by default (TopicCard defaultOpen={false}) — every test
// opens it first via its header button, matching how a real user would reach it.
function openSkillsCard() {
  fireEvent.click(screen.getByRole("button", { name: /^Skills/ }));
}

describe("JobCriteriaEditor skills list", () => {
  it("adds a skill by typing and pressing Enter", () => {
    render(<Harness />);
    openSkillsCard();

    const input = screen.getByPlaceholderText("Add a skill…");
    fireEvent.change(input, { target: { value: "Backend stack" } });
    fireEvent.keyDown(input, { key: "Enter" });

    expect(screen.getByText("Backend stack")).toBeTruthy();
    // The input clears after adding, ready for the next entry.
    expect((input as HTMLInputElement).value).toBe("");
  });

  it("adds a skill via the Add button and ignores a blank submission", () => {
    render(<Harness />);
    openSkillsCard();

    fireEvent.click(screen.getByText("+ Add")); // blank draft — no-op
    expect(screen.queryByLabelText(/Remove/)).toBeNull();

    const input = screen.getByPlaceholderText("Add a skill…");
    fireEvent.change(input, { target: { value: "Frontend stack" } });
    fireEvent.click(screen.getByText("+ Add"));

    expect(screen.getByText("Frontend stack")).toBeTruthy();
  });

  it("removes a skill via its own remove button, not only by dragging", () => {
    const initial = { ...parseJobCriteriaYaml(""), skills: ["Backend stack", "Frontend stack"] };
    render(<Harness initial={initial} />);
    openSkillsCard();

    fireEvent.click(screen.getByLabelText("Remove Backend stack"));

    expect(screen.queryByText("Backend stack")).toBeNull();
    expect(screen.getByText("Frontend stack")).toBeTruthy();
  });

  it("reorders skills with the up/down buttons — list position is priority", () => {
    const initial = { ...parseJobCriteriaYaml(""), skills: ["Backend stack", "Frontend stack", "Cloud platform"] };
    render(<Harness initial={initial} />);
    openSkillsCard();

    // Move "Frontend stack" (index 1) up, ahead of "Backend stack".
    fireEvent.click(screen.getByLabelText("Move Frontend stack up"));

    // Read order off the <li> list items specifically (role="listitem"), not a broad text
    // query — the section's own description paragraph also mentions "Cloud platform" as an
    // example, which would otherwise pollute a plain getAllByText match.
    const items = screen.getAllByRole("listitem").map(li => li.textContent);
    expect(items).toEqual([
      expect.stringContaining("Frontend stack"),
      expect.stringContaining("Backend stack"),
      expect.stringContaining("Cloud platform"),
    ]);

    // The first item's "up" button and the last item's "down" button are disabled — there's
    // nowhere further to move.
    expect(screen.getByLabelText("Move Frontend stack up")).toHaveProperty("disabled", true);
    expect(screen.getByLabelText("Move Cloud platform down")).toHaveProperty("disabled", true);
  });

  it("shows the required warning only when the skills list is empty", () => {
    render(<Harness />);
    openSkillsCard();
    expect(screen.getByText(/Required — add at least one skill\./)).toBeTruthy();

    const input = screen.getByPlaceholderText("Add a skill…");
    fireEvent.change(input, { target: { value: "Backend stack" } });
    fireEvent.keyDown(input, { key: "Enter" });

    expect(screen.queryByText(/Required — add at least one skill\./)).toBeNull();
  });
});
