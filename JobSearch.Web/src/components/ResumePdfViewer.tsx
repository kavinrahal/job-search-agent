import { useState } from "react";
import { Document, Page, pdfjs } from "react-pdf";
import "react-pdf/dist/Page/AnnotationLayer.css";
import "react-pdf/dist/Page/TextLayer.css";
import { DocumentPage, Callout } from "../ui";

// Vite-native worker resolution — bundles the exact worker matching the installed pdfjs-dist
// version as a static asset, no CDN dependency or version-mismatch risk.
pdfjs.GlobalWorkerOptions.workerSrc = new URL(
  "pdfjs-dist/build/pdf.worker.min.mjs",
  import.meta.url,
).toString();

// Our API is a separate origin from the frontend in production, so the PDF request needs the
// session cookie explicitly — defined outside the component per react-pdf's docs, since a new
// object identity on every render would otherwise reload the document each time.
const DOCUMENT_OPTIONS = { withCredentials: true };

// Renders every page as its own canvas in a horizontally-scrollable row, rather than the
// browser's native (vertical-scroll) PDF viewer — reviewing a resume is naturally page-by-page,
// not one continuous scroll.
//
// `source` accepts either a server URL (an already-saved resume, e.g. resumePdfUrl()) or a
// local File (a just-picked upload that isn't persisted until the surrounding form is saved —
// no server round-trip needed just to preview it before then).
export function ResumePdfViewer({ source }: { source: string | File }) {
  const [numPages, setNumPages] = useState(0);
  const [error, setError] = useState<string | null>(null);

  return (
    <div className="space-y-3">
      <Document
        file={source}
        options={typeof source === "string" ? DOCUMENT_OPTIONS : undefined}
        onLoadSuccess={({ numPages }) => setNumPages(numPages)}
        // Surface the real reason (e.g. a CORS/auth failure vs. a genuinely corrupt PDF look
        // completely different here) instead of a single generic message for every failure
        // mode — and suppress react-pdf's own default "Failed to load PDF file." text (the
        // `error` prop below) so we don't show two stacked, differently-worded messages for
        // the same failure.
        error=""
        onLoadError={err => setError(`Couldn't load the PDF (${err.message || err.name}).`)}
        loading={<p className="py-8 text-center text-note text-faint">Loading PDF…</p>}
      >
        <div className="flex gap-4 overflow-x-auto">
          {Array.from({ length: numPages }, (_, i) => (
            // The PDF canvas itself always stays white (it's the real document content, not
            // themeable chrome) — DocumentPage (ui) is the design system's own fixed-A4-
            // proportion preview frame, one per rendered page.
            <div key={i} className="shrink-0">
              <DocumentPage maxWidth={420}>
                <Page pageNumber={i + 1} width={420} />
              </DocumentPage>
            </div>
          ))}
        </div>
      </Document>
      {error && <Callout variant="danger" title={error} />}
    </div>
  );
}
