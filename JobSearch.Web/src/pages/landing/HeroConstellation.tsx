import { useEffect, useRef } from "react";

// Ember-tinted particle constellation drifting behind the hero copy — purely decorative. Reads
// `--color-ember` from computed styles at draw time rather than hardcoding a hex, so it tracks
// the active theme (and its dark-mode repoint in tokens.css) without a color prop of its own.
//
// Skips entirely under prefers-reduced-motion: no canvas context, no rAF loop, nothing to tear
// down. Sized to the parent element (expected to be `position: relative`), not the viewport.

function prefersReducedMotion(): boolean {
  return typeof matchMedia === "function" && matchMedia("(prefers-reduced-motion: reduce)").matches;
}

interface Particle {
  x: number;
  y: number;
  vx: number;
  vy: number;
  r: number;
}

const LINK_DISTANCE = 120;
const MAX_PARTICLES = 46;

export function HeroConstellation() {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    const container = canvas?.parentElement;
    if (!canvas || !container || prefersReducedMotion()) return;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    let particles: Particle[] = [];
    let raf = 0;

    function resize() {
      const r = container!.getBoundingClientRect();
      canvas!.width = r.width;
      canvas!.height = r.height;
      const count = Math.min(MAX_PARTICLES, Math.floor((r.width * r.height) / 18000));
      particles = Array.from({ length: count }, () => ({
        x: Math.random() * r.width,
        y: Math.random() * r.height,
        vx: (Math.random() - 0.5) * 0.35,
        vy: (Math.random() - 0.5) * 0.35,
        r: Math.random() * 1.6 + 0.6,
      }));
    }

    function step() {
      const w = canvas!.width;
      const h = canvas!.height;
      ctx!.clearRect(0, 0, w, h);
      const emberColor = getComputedStyle(document.documentElement).getPropertyValue("--color-ember").trim() || "#b03a26";

      particles.forEach(p => {
        p.x += p.vx;
        p.y += p.vy;
        if (p.x < 0 || p.x > w) p.vx *= -1;
        if (p.y < 0 || p.y > h) p.vy *= -1;
      });

      for (let i = 0; i < particles.length; i++) {
        for (let j = i + 1; j < particles.length; j++) {
          const dx = particles[i].x - particles[j].x;
          const dy = particles[i].y - particles[j].y;
          const dist = Math.sqrt(dx * dx + dy * dy);
          if (dist < LINK_DISTANCE) {
            ctx!.strokeStyle = emberColor;
            ctx!.globalAlpha = (1 - dist / LINK_DISTANCE) * 0.22;
            ctx!.lineWidth = 1;
            ctx!.beginPath();
            ctx!.moveTo(particles[i].x, particles[i].y);
            ctx!.lineTo(particles[j].x, particles[j].y);
            ctx!.stroke();
          }
        }
      }

      ctx!.globalAlpha = 0.55;
      particles.forEach(p => {
        ctx!.fillStyle = emberColor;
        ctx!.beginPath();
        ctx!.arc(p.x, p.y, p.r, 0, Math.PI * 2);
        ctx!.fill();
      });
      ctx!.globalAlpha = 1;

      raf = requestAnimationFrame(step);
    }

    window.addEventListener("resize", resize);
    resize();
    raf = requestAnimationFrame(step);
    return () => {
      window.removeEventListener("resize", resize);
      cancelAnimationFrame(raf);
    };
  }, []);

  return <canvas ref={canvasRef} aria-hidden="true" className="pointer-events-none absolute inset-0 h-full w-full opacity-55" />;
}
