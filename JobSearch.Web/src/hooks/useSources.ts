import { fetchSources, updateSources, fetchGmailForwardingStatus } from "../api";
import { useAsyncData, useAsyncAction } from "./useAsync";

export function useSources() {
  return useAsyncData(fetchSources, []);
}

export function useUpdateSources() {
  return useAsyncAction(updateSources);
}

// Only ever mounted (via the component that calls this) once Gmail is already connected —
// the endpoint 400s otherwise. `reload()` backs the manual "Check status" button.
export function useGmailForwardingStatus() {
  return useAsyncData(fetchGmailForwardingStatus, []);
}
