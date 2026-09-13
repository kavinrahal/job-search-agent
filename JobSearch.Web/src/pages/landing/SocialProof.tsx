import { Badge, Eyebrow, Ledger, LedgerRow, Surface } from "../../ui";

// The "delivered overnight" proof panel, promoted from the hero's side column (prototype section
// 1) into its own full-width section directly below it. There is no real social proof yet, no
// testimonials and no user counts, so this stays honest about what it is: a sample of real
// evaluation output, not a dressed-up customer quote.

const PROOF = [
  { company: "Victorian Government", role: "Senior Developer", badge: "Strong" as const, variant: "strong" as const },
  { company: "GHD", role: "Team Leader, Software Development", badge: "Good" as const, variant: "good" as const },
  { company: "NCS Australia", role: "Senior Software Engineer", badge: "Weak" as const, variant: "weak" as const },
];

export function SocialProof() {
  return (
    <section className="py-11">
      <Eyebrow>What it actually finds</Eyebrow>
      <h2 className="mt-2.5 mb-5 max-w-[36ch] text-[20px] leading-[1.15] font-bold tracking-[-.03em] text-balance sm:text-[25px]">
        A real evaluation, not a keyword match.
      </h2>

      <Surface elevation="raised" padding="none" clip>
        <div className="hairline-b flex items-center justify-between px-3.5 py-2.5">
          <span className="text-eyebrow text-muted uppercase">Delivered overnight</span>
          <span className="text-meta text-faint">6:12am</span>
        </div>
        <Ledger>
          {PROOF.map(item => (
            <LedgerRow
              key={item.company}
              tick="done"
              title={item.company}
              subtitle={item.role}
              meta={<Badge variant={item.variant}>{item.badge}</Badge>}
            />
          ))}
        </Ledger>
      </Surface>
      <p className="mt-2.5 text-meta text-faint">
        Example output from a real run. Every posting gets checked and scored before it reaches you.
      </p>
    </section>
  );
}
