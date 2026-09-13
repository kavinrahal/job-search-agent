import { FeaturePanel } from "../../ui";
import { Band } from "./Band";
import { Reveal } from "./Reveal";

// FeaturePanel used for its intended "product talking" case. No `stats` prop: there is no real
// product metric to show yet, and inventing one here would misrepresent it as a real number.
//
// FeaturePanel's own always-dark card is untouched; it sits on a `shell` band purely for
// background contrast behind it.

export function Solution() {
  return (
    <Band tone="shell" hairline="t">
      <Reveal>
        <FeaturePanel
          eyebrow="How Work Santa helps"
          title="One evening of setup. A shortlist waiting for you every morning after."
          subtitle="Tell it what you are looking for once: location, stack, salary, dealbreakers. It checks new postings overnight, discards anything off target, and writes a targeted CV only for the roles that clear the bar."
        />
      </Reveal>
    </Band>
  );
}
