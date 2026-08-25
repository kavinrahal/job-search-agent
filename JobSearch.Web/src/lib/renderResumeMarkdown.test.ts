import { describe, it, expect } from "vitest";
import { renderResumeMarkdown } from "./renderResumeMarkdown";

// Known input/output pairs for exactly the markdown subset ResumeRenderer.cs commits to
// producing server-side. Several inputs below are lifted directly from real ResumeRenderer
// output shapes asserted in JobSearchAgent.Tests/ResumeRendererTests.cs (e.g. the "# Jordan
// Rivers" header/contact line, "### Engineer – Acme Corp" role headings, "**Cloud** – Azure"
// skills lines) — keeping the two subset implementations' contract honest matters more here
// than most unit tests, since a silent divergence would mean the preview lies about what Save
// will actually produce.

describe("renderResumeMarkdown", () => {
  it("renders a level-1 heading", () => {
    expect(renderResumeMarkdown("# Jordan Rivers")).toBe("<h1>Jordan Rivers</h1>");
  });

  it("renders a level-2 heading with a following rule", () => {
    expect(renderResumeMarkdown("## Summary")).toBe(
      '<h2>Summary</h2><hr class="resume-section-rule" />',
    );
  });

  it("renders a level-3 heading", () => {
    expect(renderResumeMarkdown("### Engineer – Acme Corp")).toBe("<h3>Engineer – Acme Corp</h3>");
  });

  it("renders consecutive bullets as one list, supporting both '- ' and '* ' markers", () => {
    const input = "- Shipped feature A.\n- Shipped feature B.\n* Shipped feature C.";
    expect(renderResumeMarkdown(input)).toBe(
      '<ul class="resume-bullets"><li>Shipped feature A.</li><li>Shipped feature B.</li><li>Shipped feature C.</li></ul>',
    );
  });

  it("closes and reopens the bullet list around a non-bullet line", () => {
    const input = "- First\nplain text\n- Second";
    expect(renderResumeMarkdown(input)).toBe(
      '<ul class="resume-bullets"><li>First</li></ul><p>plain text</p><ul class="resume-bullets"><li>Second</li></ul>',
    );
  });

  it("renders bold spans inline within a plain line", () => {
    expect(renderResumeMarkdown("**Cloud** – Azure")).toBe("<p><strong>Cloud</strong> – Azure</p>");
  });

  it("renders bold spans inline within a bullet", () => {
    expect(renderResumeMarkdown("- **Languages** – C#, TypeScript")).toBe(
      '<ul class="resume-bullets"><li><strong>Languages</strong> – C#, TypeScript</li></ul>',
    );
  });

  it("renders a '---' line as a divider, distinct from the section rule", () => {
    expect(renderResumeMarkdown("---")).toBe('<hr class="resume-divider" />');
  });

  it("drops blank lines rather than emitting an empty element", () => {
    expect(renderResumeMarkdown("# Title\n\nBody")).toBe("<h1>Title</h1><p>Body</p>");
  });

  it("renders a contact line (plain text, no markdown syntax) as a paragraph", () => {
    expect(renderResumeMarkdown("jordan@example.com | 555-0100 | Remote")).toBe(
      "<p>jordan@example.com | 555-0100 | Remote</p>",
    );
  });

  it("escapes HTML-significant characters in plain text", () => {
    expect(renderResumeMarkdown("Built <Widget/> using C++ & Go")).toBe(
      "<p>Built &lt;Widget/&gt; using C++ &amp; Go</p>",
    );
  });

  it("escapes HTML-significant characters inside a bold span", () => {
    expect(renderResumeMarkdown("**<script>alert(1)</script>**")).toBe(
      "<p><strong>&lt;script&gt;alert(1)&lt;/script&gt;</strong></p>",
    );
  });

  it("does not treat a lone '*' or unmatched '**' as bold", () => {
    expect(renderResumeMarkdown("Grew revenue 2x * some footnote")).toBe(
      "<p>Grew revenue 2x * some footnote</p>",
    );
  });

  it("renders a full multi-section document matching ResumeRenderer's real output shape", () => {
    const markdown = [
      "# Jordan Rivers",
      "",
      "jordan@example.com | Remote",
      "",
      "## Summary",
      "",
      "Backend engineer.",
      "",
      "## Experience",
      "",
      "### Engineer – Acme Corp",
      "Remote | Jan 2022 – Jun 2024",
      "",
      "Acme makes widgets.",
      "",
      "- Shipped feature A.",
      "- Shipped feature B.",
    ].join("\n");

    const html = renderResumeMarkdown(markdown);

    expect(html).toBe(
      "<h1>Jordan Rivers</h1>" +
      "<p>jordan@example.com | Remote</p>" +
      '<h2>Summary</h2><hr class="resume-section-rule" />' +
      "<p>Backend engineer.</p>" +
      '<h2>Experience</h2><hr class="resume-section-rule" />' +
      "<h3>Engineer – Acme Corp</h3>" +
      "<p>Remote | Jan 2022 – Jun 2024</p>" +
      "<p>Acme makes widgets.</p>" +
      '<ul class="resume-bullets"><li>Shipped feature A.</li><li>Shipped feature B.</li></ul>',
    );
  });
});
