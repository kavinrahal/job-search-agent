// Applies the saved/preferred theme before first paint, avoiding a flash of the wrong theme
// that a React-side useEffect can't prevent (that only runs after the initial render).
// A separate file, not an inline <script>, because the CSP's script-src is 'self' only (no
// 'unsafe-inline') — inline scripts are silently blocked regardless of content.
(function () {
  var stored = localStorage.getItem("theme");
  var dark = stored ? stored === "dark" : matchMedia("(prefers-color-scheme: dark)").matches;
  document.documentElement.classList.toggle("dark", dark);
})();
