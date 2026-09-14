import { useState } from "react";
import { Link } from "react-router-dom";
import { useGmailForwardingStatus } from "../hooks/useSources";
import { ScreenshotSlot } from "../components/ScreenshotSlot";
import { PageHeader, Surface, Well, Button, StatusTick, Callout } from "../ui";

// The illustrated, step-by-step walkthrough for the one manual step Gmail forwarding setup
// requires. Google does not let a third-party app add a forwarding address to a personal Gmail
// account on the user's behalf (forwardingAddresses.create needs a domain-wide-delegated service
// account — see GmailSettingsClient.cs's own comment on this), so the user has to do this one part
// themselves, inside Gmail's own settings. Everything after "address verified" is automatic — the
// app installs the actual filter itself (GmailSettingsClient.EnsureJobAlertFilterAsync) the moment
// the "Check status" poll below sees it confirmed.
//
// Deliberately its own page rather than an inline expansion of GmailForwardingSetup on
// SourcesPage — that card already carries a one-paragraph version of these instructions for users
// who don't need more. This is for the user who wants the full walkthrough, screenshots included.
//
// Reuses useGmailForwardingStatus rather than re-fetching — same hook GmailForwardingSetup uses,
// just a second mounted instance (React hooks don't share state across call sites, but there's only
// ever one underlying GET, so this isn't "duplicated fetching logic," just the same one reused).
interface StepDef {
  number: number;
  title: string;
  description: string;
  imageAlt: string;
}

const STEPS: StepDef[] = [
  {
    number: 1,
    title: "Open Gmail on desktop and go to Settings",
    description:
      'Open Gmail in a desktop web browser (not the Gmail mobile app — this option is not available there). Click the gear icon in the top right, then click "See all settings".',
    imageAlt: "Gmail's gear icon menu open, showing the See all settings link",
  },
  {
    number: 2,
    title: 'Open the "Forwarding and POP/IMAP" tab',
    description: 'Across the top of the Settings page, click the "Forwarding and POP/IMAP" tab.',
    imageAlt: "Gmail Settings page with the Forwarding and POP/IMAP tab highlighted",
  },
  {
    number: 3,
    title: "Add the forwarding address below",
    description:
      'Click "Add a forwarding address", then paste in the address shown below (use the Copy button) and click "Next".',
    imageAlt: "Gmail's Add a forwarding address dialog with an email address entered",
  },
  {
    number: 4,
    title: "Confirm in Gmail's dialogs",
    description:
      'Gmail will ask you to confirm twice more — click "Proceed", then "OK". You do not need to turn on "Forward a copy of incoming mail to" further down the page — WorkSanta installs its own targeted filter automatically once the address is verified, so leave the rest of that page alone.',
    imageAlt: "Gmail's Proceed and OK confirmation dialogs for adding a forwarding address",
  },
  {
    number: 5,
    title: "Confirm the address from your inbox",
    description:
      'Gmail sends a confirmation email to the new address titled "Gmail Forwarding Confirmation - Receive Emails from ...". Open it and click the verification link inside. (If Gmail\'s dialog asks for a confirmation code instead of showing a link, the same email contains that code — copy it back into the dialog.)',
    imageAlt: "The Gmail Forwarding Confirmation email with its verification link",
  },
  {
    number: 6,
    title: "Come back here and check status",
    description:
      'Once confirmed, come back to this page and click "Check status" below. WorkSanta takes it from there automatically — no filter to set up yourself.',
    imageAlt: "WorkSanta's Sources page showing forwarding confirmed",
  },
];

export function GmailForwardingHelpPage({ hideHeader = false }: { hideHeader?: boolean } = {}) {
  const { data: status, loading, error, reload } = useGmailForwardingStatus();
  const [copied, setCopied] = useState(false);

  async function handleCopy() {
    if (!status) return;
    await navigator.clipboard.writeText(status.address);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  return (
    <div className="max-w-[720px] space-y-6">
      {!hideHeader && (
        <PageHeader
          title="Set up Gmail forwarding, step by step"
          tagline="Six steps in Gmail's own settings. WorkSanta takes over automatically once the address is confirmed."
        />
      )}

      <Link to="/sources" className="text-body font-[650] text-ember hover:text-ember-hi">
        ← Back to Sources
      </Link>

      {error && (
        <Callout variant="info" title="Connect Gmail from the Sources page first.">
          Your forwarding address only exists once Gmail is connected — head back to Sources, connect
          Gmail, then come back here for the address to fill in below.
        </Callout>
      )}

      {status && (
        <Surface padding="lg">
          <p className="mb-2 text-body font-[650] text-ink-2">Your forwarding address</p>
          <div className="flex items-center gap-2">
            <Well className="px-3 py-2 text-body text-ink-2">{status.address}</Well>
            <Button variant="ghost" size="sm" onClick={handleCopy}>
              {copied ? "Copied!" : "Copy"}
            </Button>
          </div>
          {status.status === "verified" && (
            <p className="mt-3 text-body font-[650] text-pos">✓ Forwarding confirmed. The job-alert filter is installed automatically.</p>
          )}
        </Surface>
      )}

      <div className="space-y-3.5">
        {STEPS.map(step => (
          <Surface key={step.number} padding="lg">
            <div className="mb-2.5 flex items-center gap-2.5">
              <StatusTick state="pending" number={step.number} size="lg" />
              <p className="m-0 text-body font-[650] text-ink">{step.title}</p>
            </div>
            <p className="mb-3 text-body text-muted">{step.description}</p>
            <ScreenshotSlot
              src={`/images/help/gmail-forwarding-step-${step.number}.png`}
              alt={step.imageAlt}
              placeholderLabel={`step ${step.number} — ${step.imageAlt}`}
            />
          </Surface>
        ))}
      </div>

      <Surface padding="lg">
        <div className="flex flex-wrap items-center gap-3">
          <Button onClick={reload} disabled={loading}>
            {loading ? "Checking…" : "Check status"}
          </Button>
          <span className="text-body text-muted">
            {status?.status === "verified"
              ? "Confirmed — you're all set."
              : status?.status === "pending"
                ? "Waiting for you to confirm in Gmail."
                : "Not added yet."}
          </span>
        </div>
      </Surface>
    </div>
  );
}
