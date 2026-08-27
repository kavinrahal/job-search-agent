import { cx } from "./cx";
import { CountUp } from "./CountUp";

// The dark "While you were asleep" block.
//
// It stays dark in both themes on purpose. It is not a surface, it is a figure — the one element
// on the dashboard that is the product talking rather than data being displayed, and inverting it
// in dark mode would collapse it into the page it is supposed to stand out from.

export interface FeatureStat {
  value: number;
  label: string;
}

export interface FeaturePanelProps {
  eyebrow: string;
  title: string;
  subtitle?: string;
  /** Exactly three reads best at this width; more and the numbers stop being individually legible. */
  stats?: FeatureStat[];
  className?: string;
}

export function FeaturePanel({ eyebrow, title, subtitle, stats, className }: FeaturePanelProps) {
  return (
    <div className={cx("relative overflow-hidden rounded-core bg-feat px-[18px] py-4 text-feat-ink", className)}>
      {/* Two soft gradients, light from the top right and shadow at the bottom left, so the flat
          fill picks up a sense of a lit surface rather than reading as a printed rectangle. */}
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-0"
        style={{
          background:
            "radial-gradient(115% 85% at 88% 6%, rgba(255,255,255,.11), transparent 55%), radial-gradient(90% 80% at 4% 100%, rgba(0,0,0,.22), transparent 55%)",
        }}
      />
      <div className="relative">
        <p className="m-0 text-eyebrow text-feat-dim uppercase">{eyebrow}</p>
        <h3 className="mt-[7px] mb-[3px] text-display font-bold text-balance">{title}</h3>
        {subtitle && <p className="m-0 text-note text-feat-dim">{subtitle}</p>}

        {stats && stats.length > 0 && (
          <div className="mt-[15px] grid grid-cols-3 gap-[13px]">
            {stats.map(stat => (
              <div key={stat.label}>
                <CountUp value={stat.value} className="block text-stat-sm font-bold" />
                <div className="mt-px text-meta text-feat-dim">{stat.label}</div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
