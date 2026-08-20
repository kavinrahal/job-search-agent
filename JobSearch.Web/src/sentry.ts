import * as Sentry from "@sentry/react";

// Browser-side crash reporting. Mirrors the scrubbing posture of JobSearch.Data/SentryConfig.cs
// — see that file for why this is an allowlist rather than a blocklist.
//
// The browser's exposure is different from the server's: this bundle never sees raw Gmail
// bodies, but it does hold the user's resume text, background YAML, and generated CV content
// in React state and form inputs. So the highest-risk features are the ones that record what
// the user sees or types, and they are all deliberately off below.

const EMAIL_PATTERN = /[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}/g;
const OPAQUE_TOKEN_PATTERN = /\b[A-Za-z0-9_-]{24,}\b/g;
const MAX_MESSAGE_LENGTH = 2000;

export function redact(text: string | undefined): string {
  if (!text) return "";
  const redacted = text
    .replace(EMAIL_PATTERN, "[email]")
    .replace(OPAQUE_TOKEN_PATTERN, "[redacted]");
  return redacted.length > MAX_MESSAGE_LENGTH
    ? `${redacted.slice(0, MAX_MESSAGE_LENGTH)}…[truncated]`
    : redacted;
}

export function initSentry() {
  const dsn = import.meta.env.VITE_SENTRY_DSN;
  // Absent DSN = disabled. Normal for local dev and for any build that hasn't been given one.
  if (!dsn) return;

  Sentry.init({
    dsn,
    environment: import.meta.env.MODE === "development" ? "development" : "production",
    sendDefaultPii: false,
    // No performance tracing — this pipeline only cares about crashes, and traces burn quota.
    tracesSampleRate: 0,

    integrations: [
      // Session Replay is deliberately NOT added. It records the DOM as the user sees it,
      // which here means their resume, their background, and their generated cover letters.
      // Console breadcrumbs are off for the same reason — any console.log of component state
      // would ride along with the error. Fetch/XHR breadcrumbs stay on (method, URL, and
      // status only, never bodies) since knowing which API call failed is the single most
      // useful non-stack-trace signal for diagnosing a crash.
      Sentry.breadcrumbsIntegration({ console: false, dom: false }),
    ],

    beforeBreadcrumb(crumb) {
      if (crumb.message) crumb.message = redact(crumb.message);
      // A URL can carry an email or token in a query string.
      if (typeof crumb.data?.url === "string") crumb.data.url = redact(crumb.data.url);
      return crumb;
    },

    beforeSend(event) {
      if (event.request) {
        delete event.request.data;
        delete event.request.cookies;
        delete event.request.headers;
        if (event.request.url) event.request.url = redact(event.request.url);
      }
      if (event.user) {
        // Keep the opaque id so "how many users hit this" still works; drop everything that
        // identifies who they are.
        event.user = { id: event.user.id };
      }
      delete event.extra;
      if (event.message) event.message = redact(event.message);
      for (const ex of event.exception?.values ?? []) {
        ex.value = redact(ex.value);
      }
      return event;
    },
  });
}
