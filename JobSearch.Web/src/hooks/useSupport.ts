import { submitSupportMessage } from "../api";
import { useAsyncAction } from "./useAsync";

export function useSupportMessage() {
  return useAsyncAction(submitSupportMessage);
}
