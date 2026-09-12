import type { MissingCriteriaField } from "../lib/criteriaCompleteness";
import { Button, EmptyState, SlidersIcon, Surface } from "../ui";

// Blocks Discover until job criteria is complete — Discover's whole point is ranking postings
// against job criteria, so it's meaningless (and was previously silently confusing — an
// empty/junk list with no explanation) until criteria is actually filled in. The caller
// (DiscoveriesPage) computes `missing` via the same getMissingCriteriaFields check
// JobCriteriaEditor, JobCriteriaPage, and DashboardPage's nudge banner already use, so this can
// never disagree with any of them about what's missing. Most users can't reach this state at all
// now that the full editor blocks Save until criteria is complete (see JobCriteriaPage) — this
// mainly catches accounts that had partial criteria saved before that enforcement existed.
//
// Its own file (not inlined in DiscoveriesPage.tsx) so it's a lightweight, pure/prop-driven
// component to import in tests — DiscoveriesPage.tsx itself transitively pulls in pdf.js via
// GenerationDrawer, which breaks under jsdom.
export function DiscoverCriteriaGate({ missing }: { missing: MissingCriteriaField[] }) {
  return (
    <Surface elevation="raised">
      <EmptyState
        icon={<SlidersIcon />}
        tone="ember"
        title="Finish your job criteria to start discovering matches"
        body={`Discover uses it to know what you're looking for. Missing: ${missing.map(m => m.label).join(", ")}.`}
        action={<Button href="/criteria">Finish criteria</Button>}
      />
    </Surface>
  );
}
