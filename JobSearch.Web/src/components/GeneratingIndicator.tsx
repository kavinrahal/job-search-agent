import { useEffect, useState } from "react";
import { Surface, BrandGlyph } from "../ui";

const CV_MESSAGES = [
  "Reading your background…",
  "Matching your skills to the role…",
  "Reordering experience by relevance…",
  "Tightening the summary…",
  "Making sure the dates line up…",
  "Proofreading…",
  "Almost there…",
];

const LETTER_MESSAGES = [
  "Reading the job posting…",
  "Finding the angle that actually fits…",
  "Drafting an opening line worth reading…",
  "Cutting anything that sounds like a form letter…",
  "Proofreading…",
  "Almost there…",
];

// Re-keying the message on every rotation (rather than just swapping text) replays the
// fade-in animation each time, which is what actually sells "still working" over a static
// spinner — a spinner just spins, this visibly changes what it's telling you.
//
// ponytail: this looked like the same "hydrate local state from a source" idiom as the six
// useSyncedState call sites, but it isn't — `kind` is fixed for the component's whole mount at
// both call sites (GeneratePage renders separate cv/letter instances; GenerationDrawer's kind is
// set when the drawer opens), so there's no live "source" to resync from. The tick counter below
// just replaces the old setIndex(0)-in-effect reset with an ever-incrementing count, and the
// wraparound moves to render time via modulo — so switching `messages.length` (if kind ever did
// change under a live mount) can't index out of bounds either. No open upgrade path needed.
export function GeneratingIndicator({ kind }: { kind: "cv" | "letter" }) {
  const messages = kind === "cv" ? CV_MESSAGES : LETTER_MESSAGES;
  const [tick, setTick] = useState(0);
  const index = tick % messages.length;

  useEffect(() => {
    const id = setInterval(() => setTick(t => t + 1), 2200);
    return () => clearInterval(id);
  }, []);

  return (
    <Surface elevation="raised" className="animate-fade-in-up flex items-center gap-3">
      <div className="flex h-9 w-9 shrink-0 animate-pulse items-center justify-center rounded-core bg-ember text-on-ember">
        <BrandGlyph className="h-5 w-5" />
      </div>
      <p key={index} className="animate-fade-in text-body text-muted">
        {/* index = tick % messages.length, always in [0, messages.length - 1]. */}
        {/* eslint-disable-next-line security/detect-object-injection */}
        {messages[index]}
      </p>
    </Surface>
  );
}
