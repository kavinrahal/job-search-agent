import { fetchProfile, updateProfile, parseResumeText, parseResumePdf } from "../api";
import { useAsyncData, useAsyncAction } from "./useAsync";

export function useProfile() {
  return useAsyncData(fetchProfile, []);
}

export function useUpdateProfile() {
  return useAsyncAction(updateProfile);
}

export function useParseResumeText() {
  return useAsyncAction(parseResumeText);
}

export function useParseResumePdf() {
  return useAsyncAction(parseResumePdf);
}
