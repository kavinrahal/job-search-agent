import { PageTagline } from "../components/PageTagline";

function Section({ title, tier2, children }: { title: string; tier2?: boolean; children: React.ReactNode }) {
  return (
    <div className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
      <h3 className="mb-2 text-sm font-semibold text-gray-700">
        {title}
        {tier2 && <span className="ml-2 rounded-full bg-blue-50 px-2 py-0.5 text-xs font-medium text-blue-600">Tier 2</span>}
      </h3>
      <div className="space-y-2 text-sm text-gray-600">{children}</div>
    </div>
  );
}

export function HelpPage() {
  return (
    <div className="space-y-6">
      <h2 className="text-lg font-semibold text-gray-700">Help</h2>
      <PageTagline>How everything fits together, in plain language.</PageTagline>

      <Section title="Getting started">
        <p>
          Your Background (work history, education, skills) drives every CV, cover letter, and
          answer we generate, keep it complete and up to date on the Profile page. Job Criteria
          (what you're looking for) matters most on Tier 2, it's what automatic discovery
          matching is evaluated against.
        </p>
      </Section>

      <Section title="Generating a CV, cover letter, or answer">
        <p>
          Paste a job posting's URL, or paste the description text directly if the link can't
          be fetched (common on Seek/LinkedIn/Jora). "Ask a question" answers a specific
          application question (e.g. "Why do you want to work here?") using your background
          and the posting.
        </p>
        <p>
          Once generated, you can ask for changes in plain language, revising counts as
          another generation.
        </p>
      </Section>

      <Section title="Choosing sources and connecting Gmail" tier2>
        <p>
          Automatic sources need no setup, we search them directly. Alert-based sources need
          a job alert already saved on that platform, forwarded to us via Gmail.
        </p>
        <p>
          Connecting Gmail only grants filter/settings management, never inbox reading,
          unless you separately choose full-access tracking below.
        </p>
      </Section>

      <Section title="Application tracking modes" tier2>
        <p>
          Three choices, on the Sources page: <strong>Full inbox access</strong> reads your
          inbox to catch any status change automatically. <strong>Filter only</strong> never
          reads your inbox, it just forwards mail from a company's domain once you log an
          application. <strong>Manual</strong> means no automation at all, you track everything
          yourself. You can change this anytime.
        </p>
      </Section>

      <Section title="Discoveries and recommendations" tier2>
        <p>
          Postings found automatically, ranked against your criteria. Strong/Good match are
          worth reviewing, Weak match is a stretch, Discard didn't meet your criteria and is
          hidden by default.
        </p>
      </Section>

      <Section title="Application statuses" tier2>
        <p>
          Applied, Acknowledged, Screening, Interviewing, FinalRound, then Offer or Rejected,
          in that order for automatic updates. Ghosted and Withdrawn are set manually. You can
          change any application's status yourself at any time, including out of order.
        </p>
      </Section>

      <Section title="Credits">
        <p>
          Generating or revising a CV, cover letter, or answer uses one credit each time.
        </p>
      </Section>
    </div>
  );
}
