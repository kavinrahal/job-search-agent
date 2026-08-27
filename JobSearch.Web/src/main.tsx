import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import "./index.css";
import App from "./App";
import { ThemeProvider } from "./ui";
import { initSentry } from "./sentry";

// Before render, so a crash during the first paint is still captured.
initSentry();

const root = createRoot(document.getElementById("root")!);

// The design system gallery (src/gallery), mounted ahead of the app rather than as a route inside
// it, for two reasons: it needs no session, and App's own routing would otherwise bounce an
// unauthenticated visitor to the landing page before it could render.
//
// `import.meta.env.DEV` is replaced with the literal `false` at build time, so in a production
// build this condition is statically false, the branch is dead code, and the dynamic import is
// never emitted as a chunk. The gallery is absent from the production bundle rather than merely
// unreachable within it, which is the difference between a gate and a hidden door.
if (import.meta.env.DEV && window.location.pathname === "/__gallery") {
  void import("./gallery/GalleryPage").then(({ GalleryPage }) =>
    root.render(
      <StrictMode>
        <GalleryPage />
      </StrictMode>
    )
  );
} else {
  root.render(
    <StrictMode>
      <ThemeProvider>
        <App />
      </ThemeProvider>
    </StrictMode>
  );
}
