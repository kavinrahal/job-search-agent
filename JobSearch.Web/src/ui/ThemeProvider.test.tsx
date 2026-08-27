/** @vitest-environment jsdom */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { ThemeProvider, useTheme } from "./ThemeProvider";

// The compatibility contract matters more than the toggling here: while the page-by-page swap is in
// progress, un-migrated pages and public/theme-init.js still read `html.dark` and the legacy "theme"
// key, so a ThemeProvider that only wrote its own key would cause a flash of the wrong theme on
// every reload.

let systemDark = false;
const mediaListeners = new Set<(e: MediaQueryListEvent) => void>();

function stubMatchMedia() {
  vi.stubGlobal("matchMedia", (query: string) => ({
    matches: query.includes("prefers-color-scheme: dark") ? systemDark : false,
    media: query,
    addEventListener: (_: string, fn: (e: MediaQueryListEvent) => void) => mediaListeners.add(fn),
    removeEventListener: (_: string, fn: (e: MediaQueryListEvent) => void) => mediaListeners.delete(fn),
  }));
}

function setSystemDark(dark: boolean) {
  systemDark = dark;
  for (const fn of mediaListeners) fn({ matches: dark } as MediaQueryListEvent);
}

function Probe() {
  const { preference, theme, setPreference } = useTheme();
  return (
    <div>
      <span data-testid="preference">{preference}</span>
      <span data-testid="theme">{theme}</span>
      <button type="button" onClick={() => setPreference("dark")}>
        set-dark
      </button>
      <button type="button" onClick={() => setPreference("light")}>
        set-light
      </button>
      <button type="button" onClick={() => setPreference("system")}>
        set-system
      </button>
    </div>
  );
}

beforeEach(() => {
  localStorage.clear();
  systemDark = false;
  mediaListeners.clear();
  document.documentElement.className = "";
  document.documentElement.removeAttribute("data-theme");
  stubMatchMedia();
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("ThemeProvider preference resolution", () => {
  it("defaults to following the system", () => {
    render(
      <ThemeProvider>
        <Probe />
      </ThemeProvider>,
    );
    expect(screen.getByTestId("preference").textContent).toBe("system");
    expect(screen.getByTestId("theme").textContent).toBe("light");
  });

  it("follows the system as it changes, while the preference is system", () => {
    render(
      <ThemeProvider>
        <Probe />
      </ThemeProvider>,
    );
    act(() => setSystemDark(true));
    expect(screen.getByTestId("theme").textContent).toBe("dark");
  });

  it("stops following the system once the user picks a side", () => {
    render(
      <ThemeProvider>
        <Probe />
      </ThemeProvider>,
    );
    fireEvent.click(screen.getByText("set-light"));
    act(() => setSystemDark(true));
    // An explicit choice outranks the OS, rather than being quietly overridden by it.
    expect(screen.getByTestId("theme").textContent).toBe("light");
  });
});

describe("ThemeProvider compatibility with the un-migrated app", () => {
  it("writes both the class and the data attribute", () => {
    render(
      <ThemeProvider>
        <Probe />
      </ThemeProvider>,
    );
    fireEvent.click(screen.getByText("set-dark"));

    expect(document.documentElement.classList.contains("dark")).toBe(true);
    expect(document.documentElement.getAttribute("data-theme")).toBe("dark");

    fireEvent.click(screen.getByText("set-light"));
    expect(document.documentElement.classList.contains("dark")).toBe(false);
    expect(document.documentElement.getAttribute("data-theme")).toBe("light");
  });

  it("sets color-scheme so UA widgets and scrollbars match", () => {
    render(
      <ThemeProvider>
        <Probe />
      </ThemeProvider>,
    );
    fireEvent.click(screen.getByText("set-dark"));
    expect(document.documentElement.style.colorScheme).toBe("dark");
  });

  it("mirrors the resolved theme into the legacy key theme-init.js reads", () => {
    render(
      <ThemeProvider>
        <Probe />
      </ThemeProvider>,
    );
    fireEvent.click(screen.getByText("set-dark"));

    expect(localStorage.getItem("ws-theme")).toBe("dark");
    // Resolved, never "system" — theme-init.js has to apply a class before first paint and cannot
    // interpret a preference.
    expect(localStorage.getItem("theme")).toBe("dark");
  });

  it("stores the resolved value under the legacy key even when the preference is system", () => {
    systemDark = true;
    render(
      <ThemeProvider>
        <Probe />
      </ThemeProvider>,
    );
    expect(localStorage.getItem("ws-theme")).toBe("system");
    expect(localStorage.getItem("theme")).toBe("dark");
  });

  it("inherits an existing user's choice from the legacy key on first run", () => {
    localStorage.setItem("theme", "dark");
    render(
      <ThemeProvider>
        <Probe />
      </ThemeProvider>,
    );
    // Not reset to "system": someone who already chose dark should not be silently moved off it.
    expect(screen.getByTestId("preference").textContent).toBe("dark");
  });

  it("prefers its own key over the legacy one once both exist", () => {
    localStorage.setItem("theme", "dark");
    localStorage.setItem("ws-theme", "light");
    render(
      <ThemeProvider>
        <Probe />
      </ThemeProvider>,
    );
    expect(screen.getByTestId("preference").textContent).toBe("light");
  });
});

describe("useTheme", () => {
  it("fails loudly outside a provider rather than silently rendering the wrong theme", () => {
    const spy = vi.spyOn(console, "error").mockImplementation(() => {});
    expect(() => render(<Probe />)).toThrow(/ThemeProvider/);
    spy.mockRestore();
  });
});
