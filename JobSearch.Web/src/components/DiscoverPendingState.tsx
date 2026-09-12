import { Button, EmptyState, GiftIcon, Surface } from "../ui";

// Shown once criteria is complete but Discover hasn't evaluated anything against it yet — a
// genuinely different situation from DiscoverCriteriaGate (which blocks while criteria itself
// is still incomplete). GET /discoveries only ever serves postings evaluated at or after the
// user's current JobCriteria was saved (see UserProfile.JobCriteriaUpdatedAt) — so right after
// finishing or editing criteria there's a real, expected gap before the next scheduled
// Discover run produces anything, and the list being empty here is not an error.
//
// Copy follows this app's own established voice for this exact situation — see
// GalleryComposites.tsx's "Nothing delivered yet" specimen and Tier2Dashboard's "Last run
// overnight. Next run tonight." — rather than a generic "no results" message, which is the
// wrong read here (nothing has gone wrong; the run just hasn't happened yet).
export function DiscoverPendingState() {
  return (
    <Surface elevation="raised">
      <EmptyState
        icon={<GiftIcon />}
        tone="positive"
        title="Nothing delivered yet"
        body="The next Discover run checks postings against your criteria — anything that matches will land here once it does."
        action={<Button variant="ghost" size="sm" href="/criteria">Review criteria</Button>}
      />
    </Surface>
  );
}
