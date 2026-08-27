import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";

// Theme control for the Slate system.
//
// Supersedes hooks/useTheme.ts, which only knows light/dark. This adds "system" (follow the OS and
// keep following it as the OS changes), which is the option most people actually want and the one
// the prototype's own toggle implied.
//
// Compatibility is deliberate, because the swap happens page by page and both mechanisms have to
// coexist in between:
//   - `html.dark` is toggled, so public/theme-init.js, index.css's `@custom-variant dark` and every
//     un-migrated page keep behaving exactly as they do today.
//   - `html[data-theme]` is set, because tokens.css keys the dark palette off both and the
//     prototype used this one.
//   - The resolved value is mirrored into the legacy "theme" localStorage key, so theme-init.js
//     still applies the right class before first paint and there is no flash on reload.
// The *preference* (which may be "system") lives under its own key, since "system" has no
// representation in the legacy one.

export type ThemePreference = "light" | "dark" | "system";
export type ResolvedTheme = "light" | "dark";

const PREFERENCE_KEY = "ws-theme";
/** Written by public/theme-init.js before first paint. Only ever holds a resolved value. */
const LEGACY_KEY = "theme";
const DARK_QUERY = "(prefers-color-scheme: dark)";

interface ThemeContextValue {
  /** What the user chose, which may be "system". */
  preference: ThemePreference;
  /** What is actually on screen right now. Never "system". */
  theme: ResolvedTheme;
  setPreference: (next: ThemePreference) => void;
}

const ThemeContext = createContext<ThemeContextValue | null>(null);

function readStoredPreference(): ThemePreference {
  try {
    const stored = localStorage.getItem(PREFERENCE_KEY);
    if (stored === "light" || stored === "dark" || stored === "system") return stored;
    // First run after the swap: inherit whatever the legacy toggle last stored, so an existing
    // user's choice is not silently reset to "system".
    const legacy = localStorage.getItem(LEGACY_KEY);
    if (legacy === "light" || legacy === "dark") return legacy;
  } catch {
    // Private browsing / storage disabled. Falling back to "system" is correct, not an error.
  }
  return "system";
}

function prefersDark(): boolean {
  return typeof matchMedia === "function" && matchMedia(DARK_QUERY).matches;
}

function resolve(preference: ThemePreference, systemDark: boolean): ResolvedTheme {
  if (preference === "system") return systemDark ? "dark" : "light";
  return preference;
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [preference, setPreferenceState] = useState<ThemePreference>(readStoredPreference);
  const [systemDark, setSystemDark] = useState<boolean>(prefersDark);

  // Subscribed for the component's whole life rather than only while the preference is "system".
  //
  // Only resubscribing on demand would mean the tracked value could be stale on the way back in,
  // needing a setState during the effect to resync — which is both a cascading render and the thing
  // react-hooks/set-state-in-effect exists to prevent. Staying subscribed keeps it always fresh.
  // The cost is a state update nobody sees when the OS flips while the user has an explicit
  // preference: `theme` does not change, so the memoised context value is identical and no consumer
  // re-renders.
  useEffect(() => {
    if (typeof matchMedia !== "function") return;
    const query = matchMedia(DARK_QUERY);
    const onChange = (e: MediaQueryListEvent) => setSystemDark(e.matches);
    query.addEventListener("change", onChange);
    return () => query.removeEventListener("change", onChange);
  }, []);

  const theme = resolve(preference, systemDark);

  useEffect(() => {
    const root = document.documentElement;
    root.classList.toggle("dark", theme === "dark");
    root.setAttribute("data-theme", theme);
    // Explicit rather than relying on the class alone, so form controls, scrollbars and the
    // browser's own UA widgets match the theme the app is actually showing.
    root.style.colorScheme = theme;
    try {
      localStorage.setItem(PREFERENCE_KEY, preference);
      localStorage.setItem(LEGACY_KEY, theme);
    } catch {
      // Nothing to recover from: the theme is applied, it just will not survive a reload.
    }
  }, [preference, theme]);

  // A second tab changing the theme updates this one. Handles both keys, since an un-migrated
  // page in the other tab still writes only the legacy one.
  useEffect(() => {
    const onStorage = (e: StorageEvent) => {
      if (e.key === PREFERENCE_KEY && (e.newValue === "light" || e.newValue === "dark" || e.newValue === "system")) {
        setPreferenceState(e.newValue);
      } else if (e.key === LEGACY_KEY && (e.newValue === "light" || e.newValue === "dark")) {
        setPreferenceState(e.newValue);
      }
    };
    window.addEventListener("storage", onStorage);
    return () => window.removeEventListener("storage", onStorage);
  }, []);

  const setPreference = useCallback((next: ThemePreference) => setPreferenceState(next), []);
  const value = useMemo(() => ({ preference, theme, setPreference }), [preference, theme, setPreference]);

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme(): ThemeContextValue {
  const value = useContext(ThemeContext);
  if (!value) throw new Error("useTheme must be used inside a <ThemeProvider>.");
  return value;
}
