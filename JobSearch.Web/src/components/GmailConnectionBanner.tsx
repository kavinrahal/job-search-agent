import { Link } from "react-router-dom";
import { WarningIcon } from "../ui";

// Shown on Applications and Discover — the two pages whose whole point is tracking/surfacing
// postings via Gmail — whenever /auth/me's gmailConnectionBroken flag is set (backed by
// User.GmailConnectionBrokenAt, see GmailConnectionBrokenService). The worker already sends a
// one-time reconnect email the moment this happens (JobSearchAgent/Program.cs), but that's easy
// to miss (spam filter, wrong inbox) and there was previously no in-app indication at all — a
// user could go weeks without realizing tracking had silently stopped. This is cheap to check on
// every load since the flag already rides along on the existing /auth/me bootstrap fetch.
//
// Same bg-brass-wash/WarningIcon/text tokens as GenerationResult.tsx's AccuracyWarningBanner —
// this repo's one established warning-banner convention, not a new one.
export function GmailConnectionBanner({ broken }: { broken?: boolean }) {
  if (!broken) return null;
  return (
    <div className="mb-3 flex items-start gap-2.5 rounded-ctl bg-brass-wash px-3 py-2.5 text-control text-ink-2">
      <WarningIcon className="mt-0.5 h-3.5 w-3.5 flex-none text-brass" />
      <p className="m-0 min-w-0">
        <b className="font-[650] text-ink">Gmail tracking has stopped.</b>{" "}
        Work Santa lost access to your Gmail account, so application-status tracking has paused.{" "}
        <Link to="/sources" className="font-[650] text-ember hover:text-ember-hi">
          Reconnect Gmail from Sources
        </Link>{" "}
        to resume it.
      </p>
    </div>
  );
}
