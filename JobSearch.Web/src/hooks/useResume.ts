import { fetchResume, fetchResumeTemplates, updateResume, applyResumeTemplate } from "../api";
import { useAsyncData, useAsyncAction } from "./useAsync";

export function useResume() {
  return useAsyncData(fetchResume, []);
}

// Static catalog — loaded once, same shape as any other useAsyncData list.
export function useResumeTemplates() {
  return useAsyncData(fetchResumeTemplates, []);
}

export function useUpdateResume() {
  return useAsyncAction(updateResume);
}

export function useApplyResumeTemplate() {
  return useAsyncAction(applyResumeTemplate);
}
