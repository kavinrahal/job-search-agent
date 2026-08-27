import { renderResumeMarkdown } from "../lib/renderResumeMarkdown";
import { DocumentPage } from "../ui";

// The resume builder's live preview: a fixed-width "document" that represents the real PDF
// page faithfully, matching ResumePdfViewer's and the approved prototype's own principle — the
// real PDF has a fixed page size, so this scrolls horizontally on narrow screens rather than
// shrinking or reflowing the document content to fit (the repo owner specifically corrected an
// earlier prototype pass that shrank the document on mobile instead of scrolling it — that
// detail matters here, not a nice-to-have). DocumentPage (ui) is the design system's own A4
// preview frame, built to enforce exactly this rule — see its own comment.
//
// markdown/loading/error come from useDebouncedPreview; `markdown` is kept on screen while a
// newer preview is loading (no flash-to-blank on every edit) — only the very first load shows
// the loading state in place of content.
export function ResumePreviewPane({ markdown, loading, error }: {
  markdown: string | null;
  loading: boolean;
  error: string | null;
}) {
  return (
    <div>
      {markdown ? (
        <DocumentPage maxWidth={480}>
          <div
            className="resume-preview-document h-full overflow-y-auto p-6 text-sm text-gray-800"
            // renderResumeMarkdown escapes all user-controlled text before wrapping it in the
            // handful of tags it controls — see that function's own comment.
            dangerouslySetInnerHTML={{ __html: renderResumeMarkdown(markdown) }}
          />
        </DocumentPage>
      ) : (
        <p className="py-12 text-center text-note text-faint">
          {loading ? "Loading preview…" : error ? "Couldn't load the preview." : "Nothing to preview yet."}
        </p>
      )}
      {error && markdown && (
        <p className="mt-2 text-center text-caption text-brass">
          Couldn't refresh the preview ({error}) — showing the last successful preview.
        </p>
      )}
    </div>
  );
}
