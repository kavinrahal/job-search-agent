import { fetchMe, logout, cancelAccount, upgradeToTier2 } from "../api";
import { useAsyncData, useAsyncAction } from "./useAsync";

export function useMe() {
  return useAsyncData(fetchMe, []);
}

// Absolute URL to the API's login endpoint — must not be a relative href, since the frontend
// and API are separate origins in production; a relative path would resolve against the
// frontend's own (API-less) origin instead.
export function useLoginUrl(): string {
  return `${import.meta.env.VITE_API_URL ?? ""}/api/v1/auth/login`;
}

export function useLogout() {
  return useAsyncAction(logout);
}

export function useCancelAccount() {
  return useAsyncAction(cancelAccount);
}

export function useUpgradeToTier2() {
  return useAsyncAction(upgradeToTier2);
}
