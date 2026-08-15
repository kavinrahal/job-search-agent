import { fetchSources, updateSources } from "../api";
import { useAsyncData, useAsyncAction } from "./useAsync";

export function useSources() {
  return useAsyncData(fetchSources, []);
}

export function useUpdateSources() {
  return useAsyncAction(updateSources);
}
