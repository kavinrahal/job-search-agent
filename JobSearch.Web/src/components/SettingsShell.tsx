import type { ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import { PageHeader, SettingsSubNav } from "../ui";
import { SUB_NAV_ITEMS } from "../pages/SettingsPage";

// Criteria, Sources and Help are real routes SettingsSubNav links out to (see that component's
// own comment on why), so unlike Account/Resume/Billing they don't get SettingsSubNav for free
// from SettingsPage's own layout — without this wrapper, landing on any of them drops the sub-nav
// entirely and leaves only the account-menu dropdown as a way back. This reuses the same nav list
// and grid Settings itself uses, so all six items read as one consistent section.
export function SettingsShell({
  activeKey,
  title,
  tagline,
  children,
}: {
  activeKey: string;
  title: string;
  tagline: string;
  children: ReactNode;
}) {
  const navigate = useNavigate();
  return (
    <div className="space-y-6">
      <PageHeader title={title} tagline={tagline} />
      <div className="grid grid-cols-1 items-start gap-3.5 md:grid-cols-[200px_1fr]">
        {/* Local-tab items (Account/Resume/Billing) have no href here — clicking one navigates
            back to Settings with ?tab=<key> so SettingsPage opens on (and highlights) the tab
            that was actually clicked, rather than always landing on its default. */}
        <SettingsSubNav items={SUB_NAV_ITEMS} activeKey={activeKey} onSelect={key => navigate(`/settings?tab=${key}`)} />
        <div className="min-w-0 space-y-3.5">{children}</div>
      </div>
    </div>
  );
}
