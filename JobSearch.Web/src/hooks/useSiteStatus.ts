import { fetchSiteStatus } from "../api";
import { useAsyncData } from "./useAsync";

// Loads GET /api/v1/status once on mount, independent of useMe()/auth state entirely — App.tsx
// calls this before deciding whether to mount BrowserRouter at all, so both a logged-in and a
// logged-out visitor see maintenance mode/the banner the same way. loading stays true only for
// the very first fetch; a null `data` after that just means the request failed (network blip,
// API mid-deploy) and the app renders normally rather than blocking forever on a status check.
export function useSiteStatus() {
  return useAsyncData(fetchSiteStatus, []);
}
