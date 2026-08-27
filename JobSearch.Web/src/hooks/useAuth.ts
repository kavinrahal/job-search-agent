import {
  fetchMe,
  logout,
  cancelAccount,
  upgradeToTier2,
  inviteToTier2,
  register,
  login,
  requestPasswordReset,
  resetPassword,
} from "../api";
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

export function useInviteToTier2() {
  return useAsyncAction(inviteToTier2);
}

// --- Email/password auth, the second login path alongside useLoginUrl's Google redirect above.
// Named usePasswordLogin rather than useLogin so it reads as distinct from useLoginUrl at every
// call site — the two are peers, and neither is "the" login.

export function useRegister() {
  return useAsyncAction(register);
}

export function usePasswordLogin() {
  return useAsyncAction(login);
}

export function useRequestPasswordReset() {
  return useAsyncAction(requestPasswordReset);
}

export function useResetPassword() {
  return useAsyncAction(resetPassword);
}
