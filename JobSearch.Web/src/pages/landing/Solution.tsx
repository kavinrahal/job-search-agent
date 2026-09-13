import { FeaturePanel } from "../../ui";

// FeaturePanel used for its intended "product talking" case. No `stats` prop: there is no real
// product metric to show yet, and inventing one here would misrepresent it as a real number.

export function Solution() {
  return (
    <section className="hairline-t py-11">
      <FeaturePanel
        eyebrow="How Work Santa helps"
        title="One evening of setup. A shortlist waiting for you every morning after."
        subtitle="Tell it what you are looking for once: location, stack, salary, dealbreakers. It checks new postings overnight, discards anything off target, and writes a targeted CV only for the roles that clear the bar."
      />
    </section>
  );
}
