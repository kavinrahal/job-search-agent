import { Surface, WarningIcon } from "../ui";

// The full-page "we'll be right back" notice. App.tsx renders this in place of everything
// else — BrowserRouter, AuthedApp, LoggedOutRoutes never mount — the moment useSiteStatus()
// reports maintenanceMode true, so it works identically for a signed-in and a logged-out
// visitor. Deliberately standalone (no AppShell/nav/theme toggle): none of that chrome is
// meaningful when the app underneath it may not even have a working database connection.
export function MaintenanceNotice({ message }: { message: string | null }) {
  return (
    <div className="grid min-h-screen place-items-center bg-bg px-4 text-ink">
      <Surface elevation="raised" padding="xl" className="max-w-[440px] text-center">
        <span
          aria-hidden="true"
          className="mx-auto mb-3 grid h-[42px] w-[42px] place-items-center rounded-core bg-brass-wash text-brass [&>svg]:h-[19px] [&>svg]:w-[19px]"
        >
          <WarningIcon />
        </span>
        <h1 className="m-0 text-title font-bold text-balance text-ink">We&apos;ll be right back</h1>
        <p className="m-0 mt-2 text-body text-pretty text-muted">
          {message ?? "We're doing some quick maintenance. Check back shortly."}
        </p>
      </Surface>
    </div>
  );
}
