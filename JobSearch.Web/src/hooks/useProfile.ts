import { fetchProfile, updateProfile, parseResumePdf, uploadResumePdf } from "../api";
import { useAsyncData, useAsyncAction } from "./useAsync";

export function useProfile() {
  return useAsyncData(fetchProfile, []);
}

export function useUpdateProfile() {
  return useAsyncAction(updateProfile);
}

export function useParseResumePdf() {
  return useAsyncAction(parseResumePdf);
}

export function useUploadResumePdf() {
  return useAsyncAction(uploadResumePdf);
}
