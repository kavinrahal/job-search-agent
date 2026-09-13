import type { RefObject } from "react";
import { gsap } from "gsap";
import { ScrollTrigger } from "gsap/ScrollTrigger";
import { useGSAP } from "@gsap/react";

// Prototype: the How It Works timeline's connecting rule "draws in" (grows from 0 to full
// length) as each step scrolls into view, one step at a time rather than all together. Extended
// per the reviewed mockup with two more pieces the first prototype pass didn't have yet: a small
// glowing pulse that travels down each connector right after it draws, and a 3D flip-in on every
// step's dot instead of a plain pop.
//
// This whole module only exists behind the dynamic import in HowItWorks.tsx, so GSAP +
// ScrollTrigger ship as their own chunk that loads for a landing page visit and never for the
// authenticated app (dashboard/etc. never reference this file, or HowItWorks, at all).
//
// DrawSVG was the plan going in, but it animates an SVG `<path>`/`<line>`'s stroke — the actual
// connecting rule (see Timeline.tsx) is a plain CSS border on a <span>, not SVG. Converting the
// shared Timeline component's markup to SVG just for this one placement would be a much bigger,
// riskier change than reaching for it here would be worth for a first prototype. A scaleY
// grow-from-top transform, driven by the same GSAP + ScrollTrigger stack, reads as the same
// "line drawing in" effect without touching Timeline's markup beyond a data-attribute hook.
gsap.registerPlugin(ScrollTrigger);

const CONNECTOR_SELECTOR = "[data-timeline-connector]";
const PULSE_SELECTOR = "[data-timeline-pulse]";
const DOT_SELECTOR = "[data-timeline-dot]";

export interface TimelineDrawProps {
  /** The element wrapping the Timeline whose connectors should animate in on scroll. */
  containerRef: RefObject<HTMLElement | null>;
}

export function TimelineDraw({ containerRef }: TimelineDrawProps) {
  useGSAP(
    () => {
      const container = containerRef.current;
      if (!container) return;

      // Every step's dot flips in with a 3D rotate as it scrolls into view, including the last
      // step, which has no connector to draw — so this iterates dots independently of connectors
      // rather than piggybacking on the connector loop below.
      container.querySelectorAll<HTMLElement>(DOT_SELECTOR).forEach(dot => {
        const item = dot.closest("li");
        gsap.set(dot, { transformPerspective: 600, scale: 0, rotateY: 90 });
        gsap.to(dot, {
          scale: 1,
          rotateY: 0,
          duration: 0.5,
          ease: "back.out(1.7)",
          scrollTrigger: { trigger: item ?? dot, start: "top 82%" },
        });
      });

      const connectors = container.querySelectorAll<HTMLElement>(CONNECTOR_SELECTOR);
      connectors.forEach(connector => {
        const item = connector.closest("li");
        gsap.set(connector, { scaleY: 0, transformOrigin: "top" });
        gsap.to(connector, {
          scaleY: 1,
          duration: 0.6,
          ease: "power2.out",
          scrollTrigger: {
            // The step whose own rule this is, not the next one it connects to — the line grows
            // downward out of the step that just scrolled into view.
            trigger: item ?? connector,
            start: "top 78%",
            // No toggleActions override: GSAP's default (play on enter, do nothing on every other
            // direction) means each rule draws in once and stays drawn, matching how CountUp
            // elsewhere on this page never re-animates a stat the reader has already seen.
          },
        });

        // A small pulse travels the length of the connector right after it draws in, reading as
        // the line "delivering" something down to the next step. Same trigger/start as the
        // connector's own draw so the two stay visually paired.
        const pulse = connector.querySelector<HTMLElement>(PULSE_SELECTOR);
        if (!pulse) return;
        gsap.fromTo(
          pulse,
          { top: "0%", opacity: 1 },
          {
            top: "100%",
            opacity: 0,
            duration: 1.1,
            delay: 0.05,
            ease: "power1.out",
            scrollTrigger: { trigger: item ?? connector, start: "top 78%" },
          },
        );
      });
    },
    { scope: containerRef as RefObject<HTMLElement>, dependencies: [] },
  );

  return null;
}
