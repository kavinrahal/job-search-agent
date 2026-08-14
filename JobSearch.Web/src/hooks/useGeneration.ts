import { generateCv, generateLetter, askQuestion, editThread } from "../api";
import { useAsyncAction } from "./useAsync";

export function useGenerateCv() {
  return useAsyncAction(generateCv);
}

export function useGenerateLetter() {
  return useAsyncAction(generateLetter);
}

export function useAskQuestion() {
  return useAsyncAction(askQuestion);
}

export function useEditThread() {
  return useAsyncAction(editThread);
}
