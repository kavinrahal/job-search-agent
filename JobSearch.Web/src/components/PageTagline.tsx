import type { ReactNode } from "react";

export function PageTagline({ children }: { children: ReactNode }) {
  return <p className="text-sm text-gray-400">{children}</p>;
}
