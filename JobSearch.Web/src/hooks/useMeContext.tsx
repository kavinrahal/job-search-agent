import { createContext, useContext, type ReactNode } from "react";
import type { fetchMe } from "../api";

// The current user plus a way to re-fetch them, shared via context so any descendant that spends a
// credit (a CV/letter generation, a revision) can refresh the credit balance in the header right
// after — App.tsx fetches `me` once, and without this the pill stayed stale until a full reload.
// Follows the same shape/conventions as ui/ThemeProvider's ThemeContext.

export type Me = Awaited<ReturnType<typeof fetchMe>>;

interface MeContextValue {
  me: Me;
  reloadMe: () => void;
}

const MeContext = createContext<MeContextValue | null>(null);

export function MeProvider({ me, reloadMe, children }: { me: Me; reloadMe: () => void; children: ReactNode }) {
  return <MeContext.Provider value={{ me, reloadMe }}>{children}</MeContext.Provider>;
}

export function useMeContext(): MeContextValue {
  const value = useContext(MeContext);
  if (!value) throw new Error("useMeContext must be used inside a <MeProvider>.");
  return value;
}
