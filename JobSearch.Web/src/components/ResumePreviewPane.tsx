import { renderResumeMarkdown } from "../lib/renderResumeMarkdown";

// The resume builder's live preview: a fixed-width "document" that represents the real PDF
// page faithfully, matching ResumePdfViewer's and the approved prototype's own principle — the
// real PDF has a fixed page size, so this scrolls horizontally on narrow screens rather than
// shrinking or reflowing the document content to fit (the repo owner specifically corrected an
// earlier prototype pass that shrank the document on mobile instead of scrolling it — that
// detail matters here, not a nice-to-have).
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
    <div className="overflow-x-auto rounded-xl border border-gray-200 bg-gray-50 p-4 dark:border-gray-800 dark:bg-gray-900">
      {markdown ? (
        <div
          className="resume-preview-document mx-auto w-[480px] shrink-0 rounded-lg border border-gray-200 bg-white p-6 text-sm text-gray-800 shadow-sm"
          // renderResumeMarkdown escapes all user-controlled text before wrapping it in the
          // handful of tags it controls — see that function's own comment.
          dangerouslySetInnerHTML={{ __html: renderResumeMarkdown(markdown) }}
        />
      ) : (
        <p className="py-12 text-center text-sm text-gray-400 dark:text-gray-500">
          {loading ? "Loading preview…" : error ? "Couldn't load the preview." : "Nothing to preview yet."}
        </p>
      )}
      {error && markdown && (
        <p className="mt-2 text-center text-xs text-amber-600 dark:text-amber-400">
          Couldn't refresh the preview ({error}) — showing the last successful preview.
        </p>
      )}
    </div>
  );
}
