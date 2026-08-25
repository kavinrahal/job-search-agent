// Deliberately non-general markdown -> HTML, for exactly the subset ResumeRenderer.cs commits
// to producing and JobSearch.Api/Services/PdfRenderer.cs already parses server-side to build
// the real PDF: '# '/'## '/'### ' headings, '- '/'* ' bullets, '**bold**' inline spans, and a
// '---' divider. Not a general markdown library — pulling one in for a subset this small would
// be the wrong side of the ladder (see the resume-builder plan's own reasoning). The live
// preview's entire reason to exist is representing the real rendered output faithfully, so this
// intentionally tracks PdfRenderer.cs's own line-by-line rules rather than any markdown spec.
//
// Output is plain HTML built from escaped text — safe for dangerouslySetInnerHTML since every
// piece of user-controlled text is escaped before being wrapped in a tag this function controls.

const HTML_ESCAPES: Record<string, string> = {
  "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;",
};

function escapeHtml(text: string): string {
  // ch is always one of the five literal characters the regex itself matches, never external
  // input used as a key — same fixed-set lookup pattern as resumeSections.ts's sectionLabel.
  // eslint-disable-next-line security/detect-object-injection
  return text.replace(/[&<>"']/g, ch => HTML_ESCAPES[ch]);
}

// "**bold**" -> <strong>...</strong>, everything else escaped verbatim. Same split-on-bold
// regex PdfRenderer.Inline uses server-side, so both stay in lockstep about what counts as bold.
function renderInline(text: string): string {
  return text
    .split(/(\*\*[^*]+\*\*)/)
    .map(part =>
      part.startsWith("**") && part.endsWith("**") && part.length > 4
        ? `<strong>${escapeHtml(part.slice(2, -2))}</strong>`
        : escapeHtml(part),
    )
    .join("");
}

export function renderResumeMarkdown(markdown: string): string {
  const lines = markdown.replace(/\r\n/g, "\n").split("\n");
  const html: string[] = [];
  let inList = false;

  function closeList() {
    if (inList) {
      html.push("</ul>");
      inList = false;
    }
  }

  for (const rawLine of lines) {
    const line = rawLine.trimEnd();

    if (line.startsWith("- ") || line.startsWith("* ")) {
      if (!inList) {
        html.push('<ul class="resume-bullets">');
        inList = true;
      }
      html.push(`<li>${renderInline(line.slice(2))}</li>`);
      continue;
    }
    closeList();

    if (line.startsWith("# ")) {
      html.push(`<h1>${escapeHtml(line.slice(2))}</h1>`);
    } else if (line.startsWith("## ")) {
      html.push(`<h2>${escapeHtml(line.slice(3))}</h2><hr class="resume-section-rule" />`);
    } else if (line.startsWith("### ")) {
      html.push(`<h3>${escapeHtml(line.slice(4))}</h3>`);
    } else if (line === "---") {
      html.push('<hr class="resume-divider" />');
    } else if (line.trim().length === 0) {
      // Blank line: spacing comes from CSS margins on the surrounding block elements, not an
      // explicit spacer element (unlike PdfRenderer's fixed-height spacer, which exists only
      // because QuestPDF has no CSS-margin equivalent to lean on).
      continue;
    } else {
      html.push(`<p>${renderInline(line)}</p>`);
    }
  }
  closeList();

  return html.join("");
}
